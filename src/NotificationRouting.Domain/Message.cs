namespace NotificationRouting.Domain;

public sealed class Message
{
    public Message(string title, string body, Importance importance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        if (!Enum.IsDefined(importance))
            throw new ArgumentOutOfRangeException(nameof(importance));

        Id = Guid.NewGuid();
        Title = title.Trim();
        Body = body;
        Importance = importance;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; }

    public string Title { get; }

    public string Body { get; }

    public Importance Importance { get; }

    public DateTimeOffset CreatedAt { get; }
}
