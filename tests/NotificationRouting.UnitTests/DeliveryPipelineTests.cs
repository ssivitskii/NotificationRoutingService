using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NotificationRouting.Application;
using NotificationRouting.Application.Abstractions;
using NotificationRouting.Domain;
using NotificationRouting.Domain.Abstractions;
using NotificationRouting.Infrastructure;
using System.Net;
using System.Threading.Channels;

namespace NotificationRouting.UnitTests;

public sealed class DeliveryPipelineTests
{
    [Fact]
    public async Task RetryableDeliveryEventuallySucceedsAndRetainsAttempts()
    {
        var recipient = new ScriptedRecipient(2, true);
        DeliveryDispatch dispatch = CreateDispatch(recipient);
        var delay = new RecordingDelay();
        var processor = new DeliveryProcessor(delay, CreateOptions(3));

        await processor.ProcessAsync(new DeliveryCommand(dispatch), CancellationToken.None);

        DeliveryTargetSnapshot target = Assert.Single(dispatch.Snapshot().Deliveries);
        Assert.Equal(DeliveryStatus.Succeeded, target.Status);
        Assert.Equal(3, target.Attempts.Count);
        Assert.Equal(2, delay.Delays.Count);
    }

    [Fact]
    public async Task ExhaustedDeliveryEntersDeadLetterAndManualRetryPreservesHistory()
    {
        var recipient = new ScriptedRecipient(1, false);
        DeliveryDispatch dispatch = CreateDispatch(recipient);
        var processor = new DeliveryProcessor(new RecordingDelay(), CreateOptions(3));
        var store = new InMemoryDeliveryStore();
        store.Add(dispatch);

        await processor.ProcessAsync(new DeliveryCommand(dispatch), CancellationToken.None);

        DeliveryTargetSnapshot failed = Assert.Single(dispatch.Snapshot().Deliveries);
        Assert.Equal(DeliveryStatus.DeadLettered, failed.Status);
        Assert.Single(store.GetDeadLetters());

        recipient.FailuresRemaining = 0;
        RetryPreparation retry = store.PrepareRetry(failed.Id);
        Assert.Equal(RetryPreparationResult.Ready, retry.Result);
        Assert.NotNull(retry.Command);
        await processor.ProcessAsync(retry.Command, CancellationToken.None);

        DeliveryTargetSnapshot succeeded = Assert.Single(dispatch.Snapshot().Deliveries);
        Assert.Equal(DeliveryStatus.Succeeded, succeeded.Status);
        Assert.Equal(2, succeeded.Attempts.Count);
        Assert.Null(succeeded.LastError);
        Assert.Empty(store.GetDeadLetters());
    }

    [Fact]
    public async Task RetryableDeliveryExhaustsConfiguredAttemptLimit()
    {
        var recipient = new ScriptedRecipient(10, true);
        DeliveryDispatch dispatch = CreateDispatch(recipient);
        var delay = new RecordingDelay();

        await new DeliveryProcessor(delay, CreateOptions(3)).ProcessAsync(
            new DeliveryCommand(dispatch),
            CancellationToken.None);

        DeliveryTargetSnapshot target = Assert.Single(dispatch.Snapshot().Deliveries);
        Assert.Equal(DeliveryStatus.DeadLettered, target.Status);
        Assert.Equal(3, target.Attempts.Count);
        Assert.Equal(2, delay.Delays.Count);
    }

    [Fact]
    public async Task WebhookRetriesDoNotRepeatIndependentLocalTargets()
    {
        var inbox = new CountingRecipient();
        var archive = new CountingRecipient();
        var webhook = new ScriptedRecipient(2, true);
        RecipientRegistration[] registrations =
        [
            new RecipientRegistration("inbox", inbox),
            new RecipientRegistration("archive", archive),
            new RecipientRegistration("webhook", webhook),
        ];
        var dispatch = new DeliveryDispatch(
            Guid.NewGuid(),
            new Message("Status", "Body", Importance.High),
            registrations);

        await new DeliveryProcessor(new RecordingDelay(), CreateOptions(3)).ProcessAsync(
            new DeliveryCommand(dispatch),
            CancellationToken.None);

        Assert.Equal(1, inbox.Deliveries);
        Assert.Equal(1, archive.Deliveries);
        Assert.Equal(3, webhook.DeliveryCalls);
    }

    [Fact]
    public async Task CancellationLeavesDeliveryQueuedWithoutSuccessfulAttempt()
    {
        DeliveryDispatch dispatch = CreateDispatch(new CancellingRecipient());
        var processor = new DeliveryProcessor(new RecordingDelay(), CreateOptions(2));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await processor.ProcessAsync(new DeliveryCommand(dispatch), cancellation.Token));

        DeliveryTargetSnapshot target = Assert.Single(dispatch.Snapshot().Deliveries);
        Assert.Equal(DeliveryStatus.Queued, target.Status);
        Assert.Empty(target.Attempts);
    }

    [Fact]
    public async Task ConcurrentIdempotentCallsExecuteOperationOnce()
    {
        var store = new InMemoryIdempotencyStore();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int executions = 0;

        async ValueTask<PublishReceipt> Operation(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref executions);
            await release.Task.WaitAsync(cancellationToken);
            return new PublishReceipt(Guid.Parse("10246b64-2a12-4832-838e-a1541a9e195f"), false);
        }

        Task<PublishReceipt> first = store.ExecuteAsync("same", "fingerprint", Operation, CancellationToken.None).AsTask();
        Task<PublishReceipt> second = store.ExecuteAsync("same", "fingerprint", Operation, CancellationToken.None).AsTask();
        release.SetResult();
        PublishReceipt[] receipts = await Task.WhenAll(first, second);

        Assert.Equal(1, executions);
        Assert.Equal(receipts[0].MessageId, receipts[1].MessageId);
        Assert.Single(receipts, receipt => receipt.IsReplay);
    }

    [Fact]
    public async Task CancelledReplayDoesNotRemoveSuccessfulSharedReservation()
    {
        var store = new InMemoryIdempotencyStore();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int executions = 0;
        async ValueTask<PublishReceipt> Operation(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref executions);
            await release.Task.WaitAsync(cancellationToken);
            return new PublishReceipt(Guid.Parse("75d54a4a-72b3-4547-b8a4-8d16f06ce01b"), false);
        }

        Task<PublishReceipt> owner = store.ExecuteAsync("key", "request", Operation, CancellationToken.None).AsTask();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await store.ExecuteAsync("key", "request", Operation, cancellation.Token));
        Task<PublishReceipt> replay = store.ExecuteAsync("key", "request", Operation, CancellationToken.None).AsTask();
        release.SetResult();

        PublishReceipt[] receipts = await Task.WhenAll(owner, replay);
        Assert.Equal(1, executions);
        Assert.True(receipts[1].IsReplay);
    }

    [Fact]
    public void DispatchStatusUsesTotalMonotonicStateMapping()
    {
        DeliveryDispatch mixed = CreateDispatch(new CountingRecipient(), new CountingRecipient());
        DeliveryTarget first = mixed.Targets[0];
        DeliveryTarget second = mixed.Targets[1];
        Assert.Equal(DispatchStatus.Queued, mixed.Snapshot().Status);

        first.MarkProcessing();
        Assert.Equal(DispatchStatus.Processing, mixed.Snapshot().Status);
        first.RecordAttempt(SuccessAttempt(1), DeliveryStatus.Succeeded);
        Assert.Equal(DispatchStatus.Processing, mixed.Snapshot().Status);
        second.RecordAttempt(FailedAttempt(1), DeliveryStatus.DeadLettered);
        Assert.Equal(DispatchStatus.PartiallyFailed, mixed.Snapshot().Status);

        DeliveryDispatch succeeded = CreateDispatch(new CountingRecipient());
        succeeded.Targets[0].RecordAttempt(SuccessAttempt(1), DeliveryStatus.Succeeded);
        Assert.Equal(DispatchStatus.Succeeded, succeeded.Snapshot().Status);
        DeliveryDispatch dead = CreateDispatch(new CountingRecipient());
        dead.Targets[0].RecordAttempt(FailedAttempt(1), DeliveryStatus.DeadLettered);
        Assert.Equal(DispatchStatus.DeadLettered, dead.Snapshot().Status);
    }

    [Fact]
    public async Task BoundedQueueHonorsCancellationAndCompletion()
    {
        var queue = new ChannelDeliveryQueue(new DeliveryOptions { ChannelCapacity = 1 });
        await queue.EnqueueAsync(new DeliveryCommand(CreateDispatch(new CountingRecipient())), CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        ValueTask blockedWrite = queue.EnqueueAsync(
            new DeliveryCommand(CreateDispatch(new CountingRecipient())),
            cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => blockedWrite.AsTask());
        queue.Complete();
        await Assert.ThrowsAsync<ChannelClosedException>(() => queue.EnqueueAsync(
            new DeliveryCommand(CreateDispatch(new CountingRecipient())),
            CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task WorkerDrainsQueuedDeliveryDuringGracefulStop()
    {
        var recipient = new CountingRecipient();
        DeliveryDispatch dispatch = CreateDispatch(recipient);
        var queue = new ChannelDeliveryQueue(new DeliveryOptions { ChannelCapacity = 1 });
        var processor = new DeliveryProcessor(new RecordingDelay(), CreateOptions(1));
        using var worker = new NotificationDeliveryWorker(
            queue,
            processor,
            NullLogger<NotificationDeliveryWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        await queue.EnqueueAsync(new DeliveryCommand(dispatch), CancellationToken.None);

        using var stopDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await worker.StopAsync(stopDeadline.Token);

        Assert.Equal(DeliveryStatus.Succeeded, Assert.Single(dispatch.Snapshot().Deliveries).Status);
        Assert.Equal(1, recipient.Deliveries);
    }

    [Fact]
    public async Task ForcedWorkerStopRecordsCancelledAttempt()
    {
        var recipient = new BlockingRecipient();
        DeliveryDispatch dispatch = CreateDispatch(recipient);
        var queue = new ChannelDeliveryQueue(new DeliveryOptions { ChannelCapacity = 1 });
        var processor = new DeliveryProcessor(new RecordingDelay(), CreateOptions(1));
        using var worker = new NotificationDeliveryWorker(
            queue,
            processor,
            NullLogger<NotificationDeliveryWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        await queue.EnqueueAsync(new DeliveryCommand(dispatch), CancellationToken.None);
        await recipient.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        using var stopDeadline = new CancellationTokenSource();

        Task stop = worker.StopAsync(stopDeadline.Token);
        stopDeadline.Cancel();
        try
        {
            await stop;
        }
        catch (OperationCanceledException) when (stopDeadline.IsCancellationRequested)
        {
        }

        DeliveryTargetSnapshot target = Assert.Single(dispatch.Snapshot().Deliveries);
        for (int poll = 0; target.Attempts.Count == 0 && poll < 100; poll++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10));
            target = Assert.Single(dispatch.Snapshot().Deliveries);
        }

        Assert.Equal(DeliveryStatus.Queued, target.Status);
        DeliveryAttempt attempt = Assert.Single(target.Attempts);
        Assert.False(attempt.Succeeded);
        Assert.True(attempt.Retryable);
        Assert.Equal("Delivery was cancelled before completion.", attempt.Error);
    }

    [Fact]
    public async Task ReusingIdempotencyKeyWithDifferentPayloadConflicts()
    {
        var store = new InMemoryIdempotencyStore();
        static ValueTask<PublishReceipt> Operation(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new PublishReceipt(Guid.NewGuid(), false));
        }

        await store.ExecuteAsync("same", "first", Operation, CancellationToken.None);

        await Assert.ThrowsAsync<IdempotencyConflictException>(async () =>
            await store.ExecuteAsync("same", "second", Operation, CancellationToken.None));
    }

    [Fact]
    public void WebhookPolicyRequiresHttpsAndAnAllowedHost()
    {
        var options = Options.Create(new WebhookOptions { AllowedHosts = ["hooks.example.test"] });
        var policy = new WebhookEndpointPolicy(options);

        Uri accepted = policy.Validate("https://hooks.example.test/notifications");

        Assert.Equal("hooks.example.test", accepted.Host);
        Assert.Throws<ArgumentException>(() => policy.Validate("http://hooks.example.test/notifications"));
        Assert.Throws<ArgumentException>(() => policy.Validate("https://other.example.test/notifications"));
    }

    [Fact]
    public async Task HttpWebhookUsesJsonAndStableDeliveryIdHeader()
    {
        var handler = new RecordingHandler();
        var services = new ServiceCollection();
        services.AddHttpClient(HttpWebhookSink.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        var sink = new HttpWebhookSink(provider.GetRequiredService<IHttpClientFactory>());
        var deliveryId = Guid.Parse("af679b3b-29c3-4adb-b291-31e74d9aacbb");

        await sink.SendAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            deliveryId,
            new Uri("https://hooks.example.test/notifications"),
            new Message("Status", "Body", Importance.High),
            CancellationToken.None);

        Assert.Equal(deliveryId.ToString("D"), handler.IdempotencyKey);
        Assert.Contains("\"deliveryId\"", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"importance\":2", handler.Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.RequestTimeout, true)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    public async Task HttpWebhookClassifiesResponseFailures(HttpStatusCode statusCode, bool retryable)
    {
        var handler = new RecordingHandler { ResponseStatusCode = statusCode };
        var services = new ServiceCollection();
        services.AddHttpClient(HttpWebhookSink.ClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        var sink = new HttpWebhookSink(provider.GetRequiredService<IHttpClientFactory>());

        DeliveryFailureException exception = await Assert.ThrowsAsync<DeliveryFailureException>(async () =>
            await sink.SendAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Uri("https://hooks.example.test/notifications"),
                new Message("Status", "Body", Importance.High),
                CancellationToken.None));

        Assert.Equal(retryable, exception.Retryable);
        Assert.Equal((int)statusCode, exception.HttpStatusCode);
    }

    private static DeliveryAttempt FailedAttempt(int number)
    {
        return new DeliveryAttempt(number, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, false, false, 400, "failed");
    }

    private static DeliveryAttempt SuccessAttempt(int number)
    {
        return new DeliveryAttempt(number, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, true, false, null, null);
    }

    private static DeliveryDispatch CreateDispatch(IRecipient recipient)
    {
        return new DeliveryDispatch(
            Guid.NewGuid(),
            new Message("Status", "Body", Importance.High),
            [new RecipientRegistration("test", recipient)]);
    }

    private static DeliveryDispatch CreateDispatch(params IRecipient[] recipients)
    {
        return new DeliveryDispatch(
            Guid.NewGuid(),
            new Message("Status", "Body", Importance.High),
            recipients.Select((recipient, index) => new RecipientRegistration($"target-{index}", recipient)));
    }

    private static DeliveryOptions CreateOptions(int attempts)
    {
        return new DeliveryOptions
        {
            MaxAutomaticAttempts = attempts,
            BaseRetryDelayMilliseconds = 1,
        };
    }

    private sealed class ScriptedRecipient : IRecipient
    {
        private readonly bool _retryable;

        public ScriptedRecipient(int failuresRemaining, bool retryable)
        {
            FailuresRemaining = failuresRemaining;
            _retryable = retryable;
        }

        public int FailuresRemaining { get; set; }

        public int DeliveryCalls { get; private set; }

        public ValueTask DeliverAsync(DeliveryContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeliveryCalls++;
            if (FailuresRemaining > 0)
            {
                FailuresRemaining--;
                throw new DeliveryFailureException("Scripted failure.", _retryable, 503);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class CountingRecipient : IRecipient
    {
        public int Deliveries { get; private set; }

        public ValueTask DeliverAsync(DeliveryContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Deliveries++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class BlockingRecipient : IRecipient
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask DeliverAsync(DeliveryContext context, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class CancellingRecipient : IRecipient
    {
        public ValueTask DeliverAsync(DeliveryContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingDelay : IDeliveryDelay
    {
        public List<TimeSpan> Delays { get; } = new();

        public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        public string? IdempotencyKey { get; private set; }

        public HttpStatusCode ResponseStatusCode { get; init; } = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            IdempotencyKey = Assert.Single(request.Headers.GetValues("Idempotency-Key"));
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(ResponseStatusCode);
        }
    }
}
