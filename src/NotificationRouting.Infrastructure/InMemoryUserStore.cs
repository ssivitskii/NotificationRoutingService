using NotificationRouting.Application.Abstractions;
using NotificationRouting.Domain;
using System.Collections.Concurrent;

namespace NotificationRouting.Infrastructure;

public sealed class InMemoryUserStore : IUserStore
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();
    private readonly ConcurrentDictionary<string, byte> _names = new(StringComparer.OrdinalIgnoreCase);

    public bool TryAdd(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!_names.TryAdd(user.Name, 0))
            return false;

        if (_users.TryAdd(user.Id, user))
            return true;

        _names.TryRemove(user.Name, out _);
        return false;
    }

    public User? Find(Guid id)
    {
        return _users.GetValueOrDefault(id);
    }
}
