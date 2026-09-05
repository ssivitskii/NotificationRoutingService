using NotificationRouting.Application;

namespace NotificationRouting.Api.Contracts;

public sealed record DeliveryAttemptResponse(
    int Number,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    bool Succeeded,
    bool Retryable,
    int? HttpStatusCode,
    string? Error)
{
    public static DeliveryAttemptResponse FromApplication(DeliveryAttempt attempt)
    {
        return new DeliveryAttemptResponse(
            attempt.Number,
            attempt.StartedAt,
            attempt.CompletedAt,
            attempt.Succeeded,
            attempt.Retryable,
            attempt.HttpStatusCode,
            attempt.Error);
    }
}
