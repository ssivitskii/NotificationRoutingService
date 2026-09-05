using NotificationRouting.Domain.Abstractions;

namespace NotificationRouting.Domain.Recipients;

public sealed class LoggingRecipient : IRecipient
{
    private readonly IRecipient _inner;
    private readonly IDeliveryLog _log;
    private readonly string _recipientName;

    public LoggingRecipient(IRecipient inner, IDeliveryLog log, string recipientName)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientName);
        _recipientName = recipientName;
    }

    public async ValueTask DeliverAsync(DeliveryContext context, CancellationToken cancellationToken)
    {
        await _inner.DeliverAsync(context, cancellationToken).ConfigureAwait(false);
        _log.Delivered(context.Message, _recipientName);
    }
}
