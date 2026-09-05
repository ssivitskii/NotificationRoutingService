using NotificationRouting.Domain.Abstractions;

namespace NotificationRouting.Application;

public sealed class DeliveryTarget
{
    private readonly List<DeliveryAttempt> _attempts = new();
    private readonly object _syncRoot = new();
    private DeliveryStatus _status = DeliveryStatus.Queued;

    public DeliveryTarget(string destination, IRecipient recipient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        Id = Guid.NewGuid();
        Destination = destination;
        Recipient = recipient ?? throw new ArgumentNullException(nameof(recipient));
    }

    public Guid Id { get; }

    public string Destination { get; }

    public IRecipient Recipient { get; }

    public int NextAttemptNumber
    {
        get
        {
            lock (_syncRoot)
            {
                return _attempts.Count + 1;
            }
        }
    }

    public void MarkProcessing()
    {
        lock (_syncRoot)
        {
            _status = DeliveryStatus.Processing;
        }
    }

    public void MarkRetryScheduled()
    {
        lock (_syncRoot)
        {
            _status = DeliveryStatus.RetryScheduled;
        }
    }

    public void RecordAttempt(DeliveryAttempt attempt, DeliveryStatus status)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        lock (_syncRoot)
        {
            _attempts.Add(attempt);
            _status = status;
        }
    }

    public bool PrepareManualRetry()
    {
        lock (_syncRoot)
        {
            if (_status != DeliveryStatus.DeadLettered)
                return false;

            _status = DeliveryStatus.Queued;
            return true;
        }
    }

    public void RestoreDeadLetter()
    {
        lock (_syncRoot)
        {
            _status = DeliveryStatus.DeadLettered;
        }
    }

    public DeliveryTargetSnapshot Snapshot()
    {
        lock (_syncRoot)
        {
            DeliveryAttempt? lastAttempt = _status == DeliveryStatus.Succeeded
                ? null
                : _attempts.LastOrDefault(attempt => !attempt.Succeeded);
            return new DeliveryTargetSnapshot(
                Id,
                Destination,
                _status,
                _attempts.ToArray(),
                lastAttempt?.Error,
                lastAttempt?.HttpStatusCode);
        }
    }
}
