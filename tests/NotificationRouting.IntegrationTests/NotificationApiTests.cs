using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NotificationRouting.Application;
using NotificationRouting.Application.Abstractions;
using NotificationRouting.Domain;
using NotificationRouting.Domain.Abstractions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace NotificationRouting.IntegrationTests;

public sealed class NotificationApiTests
{
    private static readonly string[] SecurityKeywords = ["security"];

    [Fact]
    public async Task PublishReturnsAcceptedAndCompletesAsynchronously()
    {
        using var factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory.CreateClient();
        (Guid userId, Guid topicId) = await CreateSubscriptionAsync(client);

        using HttpResponseMessage publish = await PublishAsync(
            client,
            topicId,
            "operation-1",
            "Security",
            "Review required");

        Assert.Equal(HttpStatusCode.Accepted, publish.StatusCode);
        Guid messageId = await ReadPropertyIdAsync(publish, "messageId");
        Assert.Equal($"/api/deliveries/{messageId}", publish.Headers.Location?.OriginalString);
        JsonElement dispatch = await WaitForStatusAsync(client, messageId, "Succeeded");
        Assert.Equal(2, dispatch.GetProperty("deliveries").GetArrayLength());

        JsonElement[]? inbox = await client.GetFromJsonAsync<JsonElement[]>($"/api/users/{userId}/messages");
        JsonElement[]? archive = await client.GetFromJsonAsync<JsonElement[]>("/api/archive");
        Assert.NotNull(inbox);
        Assert.Single(inbox);
        Assert.NotNull(archive);
        Assert.Single(archive);
    }

    [Fact]
    public async Task IdempotencyReplayReturnsSameMessageAndConflictForChangedPayload()
    {
        using var factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory.CreateClient();
        (Guid userId, Guid topicId) = await CreateSubscriptionAsync(client);

        using HttpResponseMessage first = await PublishAsync(client, topicId, "same-key", "Status", "Original");
        using HttpResponseMessage replay = await PublishAsync(client, topicId, "same-key", "Status", "Original");
        using HttpResponseMessage conflict = await PublishAsync(client, topicId, "same-key", "Status", "Changed");

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotency-Replayed")));
        Guid messageId = await ReadPropertyIdAsync(first, "messageId");
        Assert.Equal(messageId, await ReadPropertyIdAsync(replay, "messageId"));
        await AssertProblemAsync(conflict, HttpStatusCode.Conflict, "Idempotency conflict");
        await WaitForStatusAsync(client, messageId, "Succeeded");
        JsonElement[]? inbox = await client.GetFromJsonAsync<JsonElement[]>($"/api/users/{userId}/messages");
        JsonElement[]? archive = await client.GetFromJsonAsync<JsonElement[]>("/api/archive");
        Assert.Single(Assert.IsType<JsonElement[]>(inbox));
        Assert.Single(Assert.IsType<JsonElement[]>(archive));
    }

    [Fact]
    public async Task DeadLetterCanBeRetriedOnceThroughHttpWithoutRepeatingLocalDelivery()
    {
        using var factory = new WebhookApiFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage userResponse = await client.PostAsJsonAsync(
            "/api/users",
            new
            {
                name = "Webhook User",
                alertKeywords = Array.Empty<string>(),
                webhookUrl = "https://hooks.example.test/delivery",
            });
        Guid userId = await ReadPropertyIdAsync(userResponse, "id");
        using HttpResponseMessage topicResponse = await client.PostAsJsonAsync(
            "/api/topics",
            new { name = "Webhook Topic" });
        Guid topicId = await ReadPropertyIdAsync(topicResponse, "id");
        using HttpResponseMessage subscription = await client.PostAsJsonAsync(
            $"/api/topics/{topicId}/subscribers",
            new { userId, minimumImportance = "Low" });
        Assert.Equal(HttpStatusCode.NoContent, subscription.StatusCode);
        using HttpResponseMessage publish = await PublishAsync(client, topicId, "webhook-key", "Status", "Body");
        Guid messageId = await ReadPropertyIdAsync(publish, "messageId");
        await WaitForStatusAsync(client, messageId, "PartiallyFailed");
        JsonElement[]? deadLetters = await client.GetFromJsonAsync<JsonElement[]>("/api/deliveries/dead-letter");
        JsonElement deadLetter = Assert.Single(Assert.IsType<JsonElement[]>(deadLetters));
        Guid deliveryId = deadLetter.GetProperty("delivery").GetProperty("id").GetGuid();

        factory.Sink.Fail = false;
        factory.Sink.Block();
        Task<HttpResponseMessage> firstRetry = client.PostAsync($"/api/deliveries/{deliveryId}/retry", null);
        using HttpResponseMessage accepted = await firstRetry;
        using HttpResponseMessage duplicate = await client.PostAsync($"/api/deliveries/{deliveryId}/retry", null);

        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        Assert.Equal($"/api/deliveries/{messageId}", accepted.Headers.Location?.OriginalString);
        await AssertProblemAsync(duplicate, HttpStatusCode.Conflict, "Delivery state conflict");
        factory.Sink.Release();
        await WaitForStatusAsync(client, messageId, "Succeeded");
        JsonElement[]? inbox = await client.GetFromJsonAsync<JsonElement[]>($"/api/users/{userId}/messages");
        JsonElement[]? archive = await client.GetFromJsonAsync<JsonElement[]>("/api/archive");
        Assert.Single(Assert.IsType<JsonElement[]>(inbox));
        Assert.Single(Assert.IsType<JsonElement[]>(archive));
    }

    [Fact]
    public async Task DeliveryStatusAndRetryEndpointsReturnTypedErrors()
    {
        using var factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage missingStatus = await client.GetAsync($"/api/deliveries/{Guid.NewGuid()}");
        using HttpResponseMessage missingRetry = await client.PostAsync(
            $"/api/deliveries/{Guid.NewGuid()}/retry",
            null);

        await AssertProblemAsync(missingStatus, HttpStatusCode.NotFound, "Resource not found");
        await AssertProblemAsync(missingRetry, HttpStatusCode.NotFound, "Resource not found");
    }

    [Fact]
    public async Task MissingIdempotencyKeyAndUnsafeWebhookReturnValidationProblems()
    {
        using var factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage topicResponse = await client.PostAsJsonAsync("/api/topics", new { name = "Validation" });
        Guid topicId = await ReadPropertyIdAsync(topicResponse, "id");

        using HttpResponseMessage missingKey = await client.PostAsJsonAsync(
            $"/api/topics/{topicId}/messages",
            new { title = "Status", body = "Body", importance = "High" });
        using HttpResponseMessage unsafeWebhook = await client.PostAsJsonAsync(
            "/api/users",
            new { name = "Unsafe", alertKeywords = Array.Empty<string>(), webhookUrl = "https://unknown.test/hook" });

        Assert.Equal(HttpStatusCode.BadRequest, missingKey.StatusCode);
        Assert.Equal("application/problem+json", missingKey.Content.Headers.ContentType?.MediaType);
        await AssertProblemAsync(unsafeWebhook, HttpStatusCode.BadRequest, "Invalid request");
    }

    [Fact]
    public async Task ReadStateAndHealthEndpointsRemainAvailable()
    {
        using var factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory.CreateClient();
        (Guid userId, Guid topicId) = await CreateSubscriptionAsync(client);
        using HttpResponseMessage publish = await PublishAsync(client, topicId, "read-state", "Security", "Read me");
        Guid messageId = await ReadPropertyIdAsync(publish, "messageId");
        await WaitForStatusAsync(client, messageId, "Succeeded");

        using HttpResponseMessage firstRead = await client.PutAsync(
            $"/api/users/{userId}/messages/{messageId}/read",
            null);
        using HttpResponseMessage repeatedRead = await client.PutAsync(
            $"/api/users/{userId}/messages/{messageId}/read",
            null);
        using HttpResponseMessage health = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.NoContent, firstRead.StatusCode);
        await AssertProblemAsync(repeatedRead, HttpStatusCode.Conflict, "Message state conflict");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    private static async Task<(Guid UserId, Guid TopicId)> CreateSubscriptionAsync(HttpClient client)
    {
        using HttpResponseMessage userResponse = await client.PostAsJsonAsync(
            "/api/users",
            new { name = $"Alice-{Guid.NewGuid():N}", alertKeywords = SecurityKeywords });
        Guid userId = await ReadPropertyIdAsync(userResponse, "id");
        using HttpResponseMessage topicResponse = await client.PostAsJsonAsync(
            "/api/topics",
            new { name = $"Operations-{Guid.NewGuid():N}" });
        Guid topicId = await ReadPropertyIdAsync(topicResponse, "id");
        using HttpResponseMessage subscriptionResponse = await client.PostAsJsonAsync(
            $"/api/topics/{topicId}/subscribers",
            new { userId, minimumImportance = "High" });
        Assert.Equal(HttpStatusCode.NoContent, subscriptionResponse.StatusCode);
        return (userId, topicId);
    }

    private static async Task<HttpResponseMessage> PublishAsync(
        HttpClient client,
        Guid topicId,
        string key,
        string title,
        string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/topics/{topicId}/messages")
        {
            Content = JsonContent.Create(new { title, body, importance = "Critical" }),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> WaitForStatusAsync(HttpClient client, Guid messageId, string status)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            JsonElement dispatch = await client.GetFromJsonAsync<JsonElement>($"/api/deliveries/{messageId}");
            if (string.Equals(dispatch.GetProperty("status").GetString(), status, StringComparison.Ordinal))
                return dispatch;

            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }

        throw new Xunit.Sdk.XunitException($"Delivery '{messageId}' did not reach status '{status}'.");
    }

    private static async Task<Guid> ReadPropertyIdAsync(HttpResponseMessage response, string propertyName)
    {
        await using Stream content = await response.Content.ReadAsStreamAsync();
        using JsonDocument document = await JsonDocument.ParseAsync(content);
        return document.RootElement.GetProperty(propertyName).GetGuid();
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedTitle)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        JsonElement problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.Equal(expectedTitle, problem.GetProperty("title").GetString());
    }

    private sealed class WebhookApiFactory : WebApplicationFactory<Program>
    {
        public ControllableWebhookSink Sink { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Delivery:BaseRetryDelayMilliseconds"] = "0",
                    ["Delivery:MaxAutomaticAttempts"] = "1",
                    ["Webhooks:AllowedHosts:0"] = "hooks.example.test",
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IWebhookSink>();
                services.AddSingleton<IWebhookSink>(Sink);
                services.RemoveAll<IDeliveryDelay>();
                services.AddSingleton<IDeliveryDelay, ImmediateDelay>();
            });
        }
    }

    private sealed class ControllableWebhookSink : IWebhookSink
    {
        private TaskCompletionSource? _gate;

        public bool Fail { get; set; } = true;

        public void Block()
        {
            _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void Release()
        {
            _gate?.TrySetResult();
        }

        public async ValueTask SendAsync(
            Guid userId,
            Guid topicId,
            Guid deliveryId,
            Uri endpoint,
            Message message,
            CancellationToken cancellationToken)
        {
            if (Fail)
                throw new DeliveryFailureException("Permanent test failure.", false, 400);
            if (_gate is not null)
                await _gate.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class ImmediateDelay : IDeliveryDelay
    {
        public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }
}
