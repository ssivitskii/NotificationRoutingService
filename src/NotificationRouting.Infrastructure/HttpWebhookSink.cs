using NotificationRouting.Application;
using NotificationRouting.Domain;
using NotificationRouting.Domain.Abstractions;
using System.Net;
using System.Net.Http.Json;

namespace NotificationRouting.Infrastructure;

public sealed class HttpWebhookSink : IWebhookSink
{
    public const string ClientName = "notification-webhooks";

    private readonly IHttpClientFactory _clientFactory;

    public HttpWebhookSink(IHttpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async ValueTask SendAsync(
        Guid userId,
        Guid topicId,
        Guid deliveryId,
        Uri endpoint,
        Message message,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(new
            {
                deliveryId,
                messageId = message.Id,
                topicId,
                userId,
                message.Title,
                message.Body,
                message.Importance,
                message.CreatedAt,
            }),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", deliveryId.ToString("D"));

        HttpClient client = _clientFactory.CreateClient(ClientName);
        try
        {
            using HttpResponseMessage response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
                return;

            int statusCode = (int)response.StatusCode;
            bool retryable = response.StatusCode is HttpStatusCode.RequestTimeout
                or HttpStatusCode.TooManyRequests
                || statusCode >= StatusCodes.ServerErrorMinimum;
            throw new DeliveryFailureException(
                $"Webhook returned HTTP {statusCode}.",
                retryable,
                statusCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (DeliveryFailureException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new DeliveryFailureException("Webhook request failed.", true, null, exception);
        }
    }

    private static class StatusCodes
    {
        public const int ServerErrorMinimum = 500;
    }
}
