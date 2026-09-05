using NotificationRouting.Application.Abstractions;
using NotificationRouting.Domain;

namespace NotificationRouting.Application;

public sealed class DeliveryProcessor
{
    private readonly IDeliveryDelay _delay;
    private readonly DeliveryOptions _options;

    public DeliveryProcessor(IDeliveryDelay delay, DeliveryOptions options)
    {
        _delay = delay;
        _options = options;
    }

    public async ValueTask ProcessAsync(DeliveryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        IEnumerable<DeliveryTarget> targets = command.TargetId is Guid targetId
            ? command.Dispatch.Targets.Where(target => target.Id == targetId)
            : command.Dispatch.Targets;

        foreach (DeliveryTarget target in targets)
        {
            await ProcessTargetAsync(command.Dispatch, target, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask ProcessTargetAsync(
        DeliveryDispatch dispatch,
        DeliveryTarget target,
        CancellationToken cancellationToken)
    {
        for (int cycleAttempt = 1; cycleAttempt <= _options.MaxAutomaticAttempts; cycleAttempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            target.MarkProcessing();
            int attemptNumber = target.NextAttemptNumber;
            DateTimeOffset startedAt = DateTimeOffset.UtcNow;

            try
            {
                await target.Recipient.DeliverAsync(
                    new DeliveryContext(target.Id, dispatch.TopicId, dispatch.Message),
                    cancellationToken).ConfigureAwait(false);
                target.RecordAttempt(
                    new DeliveryAttempt(
                        attemptNumber,
                        startedAt,
                        DateTimeOffset.UtcNow,
                        true,
                        false,
                        null,
                        null),
                    DeliveryStatus.Succeeded);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                target.RecordAttempt(
                    new DeliveryAttempt(
                        attemptNumber,
                        startedAt,
                        DateTimeOffset.UtcNow,
                        false,
                        true,
                        null,
                        "Delivery was cancelled before completion."),
                    DeliveryStatus.Queued);
                throw;
            }
            catch (Exception exception)
            {
                var failure = exception as DeliveryFailureException;
                bool retryable = failure?.Retryable ?? true;
                bool retry = retryable && cycleAttempt < _options.MaxAutomaticAttempts;
                string error = failure?.Message ?? "Recipient delivery failed.";
                target.RecordAttempt(
                    new DeliveryAttempt(
                        attemptNumber,
                        startedAt,
                        DateTimeOffset.UtcNow,
                        false,
                        retryable,
                        failure?.HttpStatusCode,
                        error),
                    retry ? DeliveryStatus.RetryScheduled : DeliveryStatus.DeadLettered);

                if (!retry)
                    return;

                var multiplier = 1 << (cycleAttempt - 1);
                long delayMilliseconds = (long)_options.BaseRetryDelayMilliseconds * multiplier;
                var delay = TimeSpan.FromMilliseconds(delayMilliseconds);
                await _delay.DelayAsync(delay, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
