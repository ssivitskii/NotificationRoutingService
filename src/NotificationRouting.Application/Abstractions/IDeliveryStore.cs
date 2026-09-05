namespace NotificationRouting.Application.Abstractions;

public interface IDeliveryStore
{
    void Add(DeliveryDispatch dispatch);

    bool Remove(Guid messageId);

    DeliveryDispatch? Find(Guid messageId);

    IReadOnlyList<DeadLetterEntry> GetDeadLetters();

    RetryPreparation PrepareRetry(Guid deliveryId);

    void RestoreDeadLetter(Guid deliveryId);
}
