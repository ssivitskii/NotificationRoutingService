using NotificationRouting.Application.Abstractions;

namespace NotificationRouting.Infrastructure;

public sealed class SystemDeliveryDelay : IDeliveryDelay
{
    public async ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
    }
}
