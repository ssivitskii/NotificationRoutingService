namespace NotificationRouting.Api.Contracts;

public sealed record PublishAcceptedResponse(Guid MessageId, bool IdempotencyReplayed);
