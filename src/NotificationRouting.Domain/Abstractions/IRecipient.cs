namespace NotificationRouting.Domain.Abstractions;

public interface IRecipient
{
    ValueTask DeliverAsync(DeliveryContext context, CancellationToken cancellationToken);
}
