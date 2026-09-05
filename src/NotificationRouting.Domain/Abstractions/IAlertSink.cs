namespace NotificationRouting.Domain.Abstractions;

public interface IAlertSink
{
    void Notify(Guid userId, Message message, string keyword);
}
