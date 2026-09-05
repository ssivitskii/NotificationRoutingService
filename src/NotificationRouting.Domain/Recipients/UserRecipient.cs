using NotificationRouting.Domain.Abstractions;

namespace NotificationRouting.Domain.Recipients;

public sealed class UserRecipient : IRecipient
{
    private readonly User _user;

    public UserRecipient(User user)
    {
        _user = user ?? throw new ArgumentNullException(nameof(user));
    }

    public ValueTask DeliverAsync(DeliveryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _user.Receive(context.Message);
        return ValueTask.CompletedTask;
    }
}
