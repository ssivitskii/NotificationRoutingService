using NotificationRouting.Domain;

namespace NotificationRouting.Api.Contracts;

public sealed record UserResponse(Guid Id, string Name, IReadOnlyList<string> AlertKeywords, string? WebhookUrl)
{
    public static UserResponse FromDomain(User user)
    {
        return new UserResponse(user.Id, user.Name, user.AlertKeywords, user.WebhookEndpoint?.AbsoluteUri);
    }
}
