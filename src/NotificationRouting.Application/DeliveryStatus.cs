namespace NotificationRouting.Application;

public enum DeliveryStatus
{
    Queued,
    Processing,
    RetryScheduled,
    Succeeded,
    DeadLettered,
}
