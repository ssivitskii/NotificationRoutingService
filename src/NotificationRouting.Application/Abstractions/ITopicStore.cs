using NotificationRouting.Domain;

namespace NotificationRouting.Application.Abstractions;

public interface ITopicStore
{
    bool TryAdd(Topic topic);

    Topic? Find(Guid id);
}
