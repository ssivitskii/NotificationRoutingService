namespace NotificationRouting.Application;

public sealed record RetryPreparation(RetryPreparationResult Result, DeliveryCommand? Command);
