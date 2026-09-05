using NotificationRouting.Domain;

namespace NotificationRouting.Api.Contracts;

public sealed record TopicResponse(Guid Id, string Name)
{
    public static TopicResponse FromDomain(Topic topic)
    {
        return new TopicResponse(topic.Id, topic.Name);
    }
}
