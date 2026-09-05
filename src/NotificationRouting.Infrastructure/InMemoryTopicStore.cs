using NotificationRouting.Application.Abstractions;
using NotificationRouting.Domain;
using System.Collections.Concurrent;

namespace NotificationRouting.Infrastructure;

public sealed class InMemoryTopicStore : ITopicStore
{
    private readonly ConcurrentDictionary<Guid, Topic> _topics = new();
    private readonly ConcurrentDictionary<string, byte> _names = new(StringComparer.OrdinalIgnoreCase);

    public bool TryAdd(Topic topic)
    {
        ArgumentNullException.ThrowIfNull(topic);
        if (!_names.TryAdd(topic.Name, 0))
            return false;

        if (_topics.TryAdd(topic.Id, topic))
            return true;

        _names.TryRemove(topic.Name, out _);
        return false;
    }

    public Topic? Find(Guid id)
    {
        return _topics.GetValueOrDefault(id);
    }
}
