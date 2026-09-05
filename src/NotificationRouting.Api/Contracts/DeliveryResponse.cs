using NotificationRouting.Application;
using NotificationRouting.Domain;

namespace NotificationRouting.Api.Contracts;

public sealed record DeliveryResponse(
    Guid MessageId,
    Guid TopicId,
    string Title,
    Importance Importance,
    DateTimeOffset AcceptedAt,
    DispatchStatus Status,
    IReadOnlyList<DeliveryTargetResponse> Deliveries)
{
    public static DeliveryResponse FromApplication(DeliveryDispatchSnapshot dispatch)
    {
        return new DeliveryResponse(
            dispatch.MessageId,
            dispatch.TopicId,
            dispatch.Title,
            dispatch.Importance,
            dispatch.AcceptedAt,
            dispatch.Status,
            dispatch.Deliveries.Select(DeliveryTargetResponse.FromApplication).ToArray());
    }
}
