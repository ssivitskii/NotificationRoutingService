namespace NotificationRouting.Domain.Abstractions;

public interface IMessageFormatter
{
    string Format(Message message);
}
