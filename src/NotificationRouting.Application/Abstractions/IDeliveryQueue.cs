namespace NotificationRouting.Application.Abstractions;

public interface IDeliveryQueue
{
    ValueTask EnqueueAsync(DeliveryCommand command, CancellationToken cancellationToken);

    IAsyncEnumerable<DeliveryCommand> ReadAllAsync(CancellationToken cancellationToken);

    void Complete();
}
