using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationRouting.Application;
using NotificationRouting.Application.Abstractions;

namespace NotificationRouting.Infrastructure;

public sealed class NotificationDeliveryWorker : BackgroundService
{
    private readonly CancellationTokenSource _forcedStop = new();
    private readonly ILogger<NotificationDeliveryWorker> _logger;
    private readonly IDeliveryQueue _queue;
    private readonly DeliveryProcessor _processor;

    public NotificationDeliveryWorker(
        IDeliveryQueue queue,
        DeliveryProcessor processor,
        ILogger<NotificationDeliveryWorker> logger)
    {
        _queue = queue;
        _processor = processor;
        _logger = logger;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _queue.Complete();
        using CancellationTokenRegistration registration = cancellationToken.Register(_forcedStop.Cancel);
        try
        {
            await base.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested)
                _forcedStop.Cancel();
        }
    }

    public override void Dispose()
    {
        _forcedStop.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = stoppingToken;
        try
        {
            await foreach (DeliveryCommand command in _queue.ReadAllAsync(_forcedStop.Token))
            {
                try
                {
                    await _processor.ProcessAsync(command, _forcedStop.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (_forcedStop.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Delivery worker failed processing message {MessageId}", command.Dispatch.Message.Id);
                }
            }
        }
        catch (OperationCanceledException) when (_forcedStop.IsCancellationRequested)
        {
            _logger.LogWarning("Delivery worker stopped before draining the queue");
        }
    }
}
