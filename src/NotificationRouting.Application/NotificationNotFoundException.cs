namespace NotificationRouting.Application;

public sealed class NotificationNotFoundException : Exception
{
    public NotificationNotFoundException(string message)
        : base(message)
    {
    }
}
