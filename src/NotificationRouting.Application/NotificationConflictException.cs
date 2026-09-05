namespace NotificationRouting.Application;

public sealed class NotificationConflictException : Exception
{
    public NotificationConflictException(string message)
        : base(message)
    {
    }
}
