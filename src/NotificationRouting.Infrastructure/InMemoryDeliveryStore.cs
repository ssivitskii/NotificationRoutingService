using NotificationRouting.Application;
using NotificationRouting.Application.Abstractions;
using System.Collections.Concurrent;

namespace NotificationRouting.Infrastructure;

public sealed class InMemoryDeliveryStore : IDeliveryStore
{
    private readonly ConcurrentDictionary<Guid, DeliveryDispatch> _dispatches = new();

    public void Add(DeliveryDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);
        if (!_dispatches.TryAdd(dispatch.Message.Id, dispatch))
            throw new InvalidOperationException($"Dispatch '{dispatch.Message.Id}' already exists.");
    }

    public bool Remove(Guid messageId)
    {
        return _dispatches.TryRemove(messageId, out _);
    }

    public DeliveryDispatch? Find(Guid messageId)
    {
        return _dispatches.GetValueOrDefault(messageId);
    }

    public IReadOnlyList<DeadLetterEntry> GetDeadLetters()
    {
        return _dispatches.Values
            .SelectMany(dispatch => dispatch.Snapshot().Deliveries
                .Where(delivery => delivery.Status == DeliveryStatus.DeadLettered)
                .Select(delivery => new DeadLetterEntry(dispatch.Message.Id, dispatch.TopicId, delivery)))
            .OrderBy(entry => entry.MessageId)
            .ThenBy(entry => entry.Delivery.Id)
            .ToArray();
    }

    public RetryPreparation PrepareRetry(Guid deliveryId)
    {
        foreach (DeliveryDispatch dispatch in _dispatches.Values)
        {
            DeliveryTarget? target = dispatch.Targets.FirstOrDefault(candidate => candidate.Id == deliveryId);
            if (target is null)
                continue;

            if (!target.PrepareManualRetry())
            {
                return new RetryPreparation(RetryPreparationResult.Conflict, null);
            }

            return new RetryPreparation(
                RetryPreparationResult.Ready,
                new DeliveryCommand(dispatch, deliveryId));
        }

        return new RetryPreparation(RetryPreparationResult.NotFound, null);
    }

    public void RestoreDeadLetter(Guid deliveryId)
    {
        DeliveryTarget? target = _dispatches.Values
            .SelectMany(dispatch => dispatch.Targets)
            .FirstOrDefault(candidate => candidate.Id == deliveryId);
        target?.RestoreDeadLetter();
    }
}
