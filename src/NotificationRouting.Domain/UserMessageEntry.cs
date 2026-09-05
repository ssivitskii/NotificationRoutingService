namespace NotificationRouting.Domain;

public sealed class UserMessageEntry
{
    public UserMessageEntry(Message message)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
    }

    public Message Message { get; }

    public ReadStatus Status { get; private set; }

    internal void MarkRead()
    {
        Status = ReadStatus.Read;
    }
}
