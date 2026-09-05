using NotificationRouting.Application;

namespace NotificationRouting.Api.Contracts;

public sealed record DeadLetterResponse(
    Guid MessageId,
    Guid TopicId,
    DeliveryTargetResponse Delivery)
{
    public static DeadLetterResponse FromApplication(DeadLetterEntry entry)
    {
        return new DeadLetterResponse(
            entry.MessageId,
            entry.TopicId,
            DeliveryTargetResponse.FromApplication(entry.Delivery));
    }
}
