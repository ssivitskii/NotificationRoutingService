using NotificationRouting.Domain.Abstractions;

namespace NotificationRouting.Domain.Recipients;

public sealed class GroupRecipient : IRecipient
{
    private readonly IReadOnlyList<IRecipient> _recipients;

    public GroupRecipient(IEnumerable<IRecipient> recipients)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        _recipients = recipients.ToArray();
    }

    public async ValueTask DeliverAsync(DeliveryContext context, CancellationToken cancellationToken)
    {
        foreach (IRecipient recipient in _recipients)
        {
            await recipient.DeliverAsync(context, cancellationToken).ConfigureAwait(false);
        }
    }
}
