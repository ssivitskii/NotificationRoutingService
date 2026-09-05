namespace NotificationRouting.Application;

public sealed record DeliveryAttempt(
    int Number,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    bool Succeeded,
    bool Retryable,
    int? HttpStatusCode,
    string? Error);
