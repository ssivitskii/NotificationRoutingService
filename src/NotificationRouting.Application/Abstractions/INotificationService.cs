using NotificationRouting.Domain;

namespace NotificationRouting.Application.Abstractions;

public interface INotificationService
{
    User CreateUser(string name, IEnumerable<string>? alertKeywords, string? webhookEndpoint);

    Topic CreateTopic(string name);

    void Subscribe(Guid topicId, Guid userId, Importance minimumImportance);

    ValueTask<PublishReceipt> PublishAsync(
        Guid topicId,
        string title,
        string body,
        Importance importance,
        string idempotencyKey,
        CancellationToken cancellationToken);

    DeliveryDispatchSnapshot GetDispatch(Guid messageId);

    IReadOnlyList<DeadLetterEntry> GetDeadLetters();

    ValueTask<Guid> RetryAsync(Guid deliveryId, CancellationToken cancellationToken);

    IReadOnlyList<UserMessageEntry> GetUserMessages(Guid userId);

    IReadOnlyList<Message> GetArchive();

    OperationResult MarkRead(Guid userId, Guid messageId);
}
