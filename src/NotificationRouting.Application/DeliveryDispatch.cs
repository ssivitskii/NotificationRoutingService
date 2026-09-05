using NotificationRouting.Domain;

namespace NotificationRouting.Application;

public sealed class DeliveryDispatch
{
    private readonly DeliveryTarget[] _targets;

    public DeliveryDispatch(Guid topicId, Message message, IEnumerable<RecipientRegistration> recipients)
    {
        if (topicId == Guid.Empty)
            throw new ArgumentException("Topic ID cannot be empty.", nameof(topicId));

        ArgumentNullException.ThrowIfNull(recipients);
        TopicId = topicId;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        AcceptedAt = DateTimeOffset.UtcNow;
        _targets = recipients
            .Select(recipient => new DeliveryTarget(recipient.Destination, recipient.Recipient))
            .ToArray();
    }

    public Guid TopicId { get; }

    public Message Message { get; }

    public DateTimeOffset AcceptedAt { get; }

    public IReadOnlyList<DeliveryTarget> Targets => _targets;

    public DeliveryDispatchSnapshot Snapshot()
    {
        DeliveryTargetSnapshot[] targets = _targets.Select(target => target.Snapshot()).ToArray();
        DispatchStatus status = GetStatus(targets);
        return new DeliveryDispatchSnapshot(
            Message.Id,
            TopicId,
            Message.Title,
            Message.Importance,
            AcceptedAt,
            status,
            targets);
    }

    private static DispatchStatus GetStatus(DeliveryTargetSnapshot[] targets)
    {
        if (targets.All(target => target.Status == DeliveryStatus.Queued))
            return DispatchStatus.Queued;

        if (targets.All(target => target.Status == DeliveryStatus.Succeeded))
            return DispatchStatus.Succeeded;

        if (targets.All(target => target.Status == DeliveryStatus.DeadLettered))
            return DispatchStatus.DeadLettered;

        if (targets.All(target => target.Status is DeliveryStatus.Succeeded or DeliveryStatus.DeadLettered))
            return DispatchStatus.PartiallyFailed;

        return DispatchStatus.Processing;
    }
}
