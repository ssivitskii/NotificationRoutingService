using NotificationRouting.Domain;

namespace NotificationRouting.Api.Contracts;

public sealed record MessageResponse(
    Guid Id,
    string Title,
    string Body,
    Importance Importance,
    DateTimeOffset CreatedAt,
    ReadStatus? Status)
{
    public static MessageResponse FromDomain(Message message, ReadStatus? status = null)
    {
        return new MessageResponse(
            message.Id,
            message.Title,
            message.Body,
            message.Importance,
            message.CreatedAt,
            status);
    }
}
