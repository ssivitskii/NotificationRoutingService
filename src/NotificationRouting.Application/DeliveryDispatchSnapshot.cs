using NotificationRouting.Domain;

namespace NotificationRouting.Application;

public sealed record DeliveryDispatchSnapshot(
    Guid MessageId,
    Guid TopicId,
    string Title,
    Importance Importance,
    DateTimeOffset AcceptedAt,
    DispatchStatus Status,
    IReadOnlyList<DeliveryTargetSnapshot> Deliveries);
