using NotificationRouting.Application;
using NotificationRouting.Application.Abstractions;
using System.Threading.Channels;

namespace NotificationRouting.Infrastructure;

public sealed class ChannelDeliveryQueue : IDeliveryQueue
{
    private readonly Channel<DeliveryCommand> _channel;

    public ChannelDeliveryQueue(DeliveryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _channel = Channel.CreateBounded<DeliveryCommand>(new BoundedChannelOptions(options.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public ValueTask EnqueueAsync(DeliveryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return _channel.Writer.WriteAsync(command, cancellationToken);
    }

    public IAsyncEnumerable<DeliveryCommand> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    public void Complete()
    {
        _channel.Writer.TryComplete();
    }
}
