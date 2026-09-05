namespace NotificationRouting.Application.Abstractions;

public interface IDeliveryDelay
{
    ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}
