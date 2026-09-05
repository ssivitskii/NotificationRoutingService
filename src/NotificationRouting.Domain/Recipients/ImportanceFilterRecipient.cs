using NotificationRouting.Domain.Abstractions;

namespace NotificationRouting.Domain.Recipients;

public sealed class ImportanceFilterRecipient : IRecipient
{
    private readonly IRecipient _inner;
    private readonly Importance _minimumImportance;

    public ImportanceFilterRecipient(IRecipient inner, Importance minimumImportance)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _minimumImportance = minimumImportance;
    }

    public ValueTask DeliverAsync(DeliveryContext context, CancellationToken cancellationToken)
    {
        return context.Message.Importance >= _minimumImportance
            ? _inner.DeliverAsync(context, cancellationToken)
            : ValueTask.CompletedTask;
    }
}
