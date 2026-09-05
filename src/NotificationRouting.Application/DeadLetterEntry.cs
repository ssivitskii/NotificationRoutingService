namespace NotificationRouting.Application;

public sealed record DeadLetterEntry(Guid MessageId, Guid TopicId, DeliveryTargetSnapshot Delivery);
