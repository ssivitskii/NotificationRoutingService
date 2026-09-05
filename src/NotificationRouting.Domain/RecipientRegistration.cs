using NotificationRouting.Domain.Abstractions;

namespace NotificationRouting.Domain;

public sealed record RecipientRegistration
{
    public RecipientRegistration(string destination, IRecipient recipient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        Destination = destination;
        Recipient = recipient ?? throw new ArgumentNullException(nameof(recipient));
    }

    public string Destination { get; }

    public IRecipient Recipient { get; }
}
