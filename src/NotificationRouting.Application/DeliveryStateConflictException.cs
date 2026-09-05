namespace NotificationRouting.Application;

public sealed class DeliveryStateConflictException : Exception
{
    public DeliveryStateConflictException(string message)
        : base(message)
    {
    }
}
