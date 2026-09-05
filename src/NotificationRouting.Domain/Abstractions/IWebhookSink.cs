namespace NotificationRouting.Domain.Abstractions;

public interface IWebhookSink
{
    ValueTask SendAsync(
        Guid userId,
        Guid topicId,
        Guid deliveryId,
        Uri endpoint,
        Message message,
        CancellationToken cancellationToken);
}
