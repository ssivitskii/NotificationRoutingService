using NotificationRouting.Domain;
using NotificationRouting.Domain.Abstractions;
using System.Collections.Concurrent;

namespace NotificationRouting.Infrastructure;

public sealed class InMemoryMessageArchive : IMessageArchive
{
    private readonly ConcurrentDictionary<Guid, Message> _messages = new();
    private readonly ConcurrentQueue<Guid> _order = new();

    public void Save(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (_messages.TryAdd(message.Id, message))
            _order.Enqueue(message.Id);
    }

    public IReadOnlyList<Message> GetAll()
    {
        return _order
            .Select(id => _messages.GetValueOrDefault(id))
            .Where(message => message is not null)
            .Cast<Message>()
            .ToArray();
    }
}
