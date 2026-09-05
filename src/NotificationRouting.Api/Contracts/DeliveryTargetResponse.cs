using NotificationRouting.Application;

namespace NotificationRouting.Api.Contracts;

public sealed record DeliveryTargetResponse(
    Guid Id,
    string Destination,
    DeliveryStatus Status,
    IReadOnlyList<DeliveryAttemptResponse> Attempts,
    string? LastError,
    int? LastHttpStatusCode)
{
    public static DeliveryTargetResponse FromApplication(DeliveryTargetSnapshot delivery)
    {
        return new DeliveryTargetResponse(
            delivery.Id,
            delivery.Destination,
            delivery.Status,
            delivery.Attempts.Select(DeliveryAttemptResponse.FromApplication).ToArray(),
            delivery.LastError,
            delivery.LastHttpStatusCode);
    }
}
