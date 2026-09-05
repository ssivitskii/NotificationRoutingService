using NotificationRouting.Domain.Abstractions;

namespace NotificationRouting.Domain;

public sealed class Topic
{
    private readonly object _syncRoot = new();
    private readonly List<RecipientRegistration> _recipients = new();
    private readonly HashSet<Guid> _subscriberIds = new();

    public Topic(string name, IRecipient archiveRecipient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = Guid.NewGuid();
        Name = name.Trim();
        _recipients.Add(new RecipientRegistration(
            "archive",
            archiveRecipient ?? throw new ArgumentNullException(nameof(archiveRecipient))));
    }

    public Guid Id { get; }

    public string Name { get; }

    public bool Subscribe(Guid subscriberId, IRecipient recipient)
    {
        return Subscribe(subscriberId, [new RecipientRegistration($"user:{subscriberId}", recipient)]);
    }

    public bool Subscribe(Guid subscriberId, IEnumerable<RecipientRegistration> recipients)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        RecipientRegistration[] snapshot = recipients.ToArray();
        if (snapshot.Length == 0)
            throw new ArgumentException("At least one recipient is required.", nameof(recipients));

        lock (_syncRoot)
        {
            if (!_subscriberIds.Add(subscriberId))
                return false;

            _recipients.AddRange(snapshot);
            return true;
        }
    }

    public IReadOnlyList<RecipientRegistration> SnapshotRecipients()
    {
        lock (_syncRoot)
        {
            return _recipients.ToArray();
        }
    }
}
