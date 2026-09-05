namespace NotificationRouting.Application;

public sealed class DeliveryQueueUnavailableException : Exception
{
    public DeliveryQueueUnavailableException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
