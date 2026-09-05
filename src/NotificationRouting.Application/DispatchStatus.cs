namespace NotificationRouting.Application;

public enum DispatchStatus
{
    Queued,
    Processing,
    Succeeded,
    PartiallyFailed,
    DeadLettered,
}
