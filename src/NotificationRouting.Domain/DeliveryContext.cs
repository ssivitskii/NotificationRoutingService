namespace NotificationRouting.Domain;

public sealed record DeliveryContext(Guid DeliveryId, Guid TopicId, Message Message);
