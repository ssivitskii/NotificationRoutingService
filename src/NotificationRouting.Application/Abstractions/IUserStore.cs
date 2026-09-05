using NotificationRouting.Domain;

namespace NotificationRouting.Application.Abstractions;

public interface IUserStore
{
    bool TryAdd(User user);

    User? Find(Guid id);
}
