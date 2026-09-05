namespace NotificationRouting.Domain;

public sealed class User
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Guid, UserMessageEntry> _inbox = new();

    public User(string name, IEnumerable<string>? alertKeywords = null, Uri? webhookEndpoint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = Guid.NewGuid();
        Name = name.Trim();
        AlertKeywords = alertKeywords?
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Select(keyword => keyword.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
        WebhookEndpoint = webhookEndpoint;
    }

    public Guid Id { get; }

    public string Name { get; }

    public IReadOnlyList<string> AlertKeywords { get; }

    public Uri? WebhookEndpoint { get; }

    public IReadOnlyList<UserMessageEntry> GetInbox()
    {
        lock (_syncRoot)
        {
            return _inbox.Values.OrderBy(entry => entry.Message.CreatedAt).ToArray();
        }
    }

    public void Receive(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (_syncRoot)
        {
            _inbox.TryAdd(message.Id, new UserMessageEntry(message));
        }
    }

    public OperationResult MarkRead(Guid messageId)
    {
        lock (_syncRoot)
        {
            if (!_inbox.TryGetValue(messageId, out UserMessageEntry? entry))
            {
                return OperationResult.Failure(
                    OperationErrorKind.NotFound,
                    $"Message '{messageId}' was not found.");
            }

            if (entry.Status == ReadStatus.Read)
                return OperationResult.Failure(OperationErrorKind.Conflict, "Message is already read.");

            entry.MarkRead();
            return OperationResult.Success();
        }
    }
}
