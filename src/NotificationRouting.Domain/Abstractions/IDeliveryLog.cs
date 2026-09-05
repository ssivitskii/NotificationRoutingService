namespace NotificationRouting.Domain.Abstractions;

public interface IDeliveryLog
{
    void Delivered(Message message, string recipientName);
}
