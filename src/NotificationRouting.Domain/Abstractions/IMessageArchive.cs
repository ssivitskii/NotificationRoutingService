namespace NotificationRouting.Domain.Abstractions;

public interface IMessageArchive
{
    void Save(Message message);

    IReadOnlyList<Message> GetAll();
}
