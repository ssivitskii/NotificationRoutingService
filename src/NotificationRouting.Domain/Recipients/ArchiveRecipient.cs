using NotificationRouting.Domain.Abstractions;

namespace NotificationRouting.Domain.Recipients;

public sealed class ArchiveRecipient : IRecipient
{
    private readonly IMessageArchive _archive;

    public ArchiveRecipient(IMessageArchive archive)
    {
        _archive = archive ?? throw new ArgumentNullException(nameof(archive));
    }

    public ValueTask DeliverAsync(DeliveryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _archive.Save(context.Message);
        return ValueTask.CompletedTask;
    }
}
