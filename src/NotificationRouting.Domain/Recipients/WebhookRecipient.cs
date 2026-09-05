using NotificationRouting.Domain.Abstractions;

namespace NotificationRouting.Domain.Recipients;

public sealed class WebhookRecipient : IRecipient
{
    private readonly Uri _endpoint;
    private readonly IWebhookSink _sink;
    private readonly Guid _userId;

    public WebhookRecipient(Guid userId, Uri endpoint, IWebhookSink sink)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID cannot be empty.", nameof(userId));

        _userId = userId;
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    public ValueTask DeliverAsync(DeliveryContext context, CancellationToken cancellationToken)
    {
        return _sink.SendAsync(
            _userId,
            context.TopicId,
            context.DeliveryId,
            _endpoint,
            context.Message,
            cancellationToken);
    }
}
