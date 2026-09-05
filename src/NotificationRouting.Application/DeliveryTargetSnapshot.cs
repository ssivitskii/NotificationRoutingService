namespace NotificationRouting.Application;

public sealed record DeliveryTargetSnapshot(
    Guid Id,
    string Destination,
    DeliveryStatus Status,
    IReadOnlyList<DeliveryAttempt> Attempts,
    string? LastError,
    int? LastHttpStatusCode);
