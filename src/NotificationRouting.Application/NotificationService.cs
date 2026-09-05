using NotificationRouting.Application.Abstractions;
using NotificationRouting.Domain;
using NotificationRouting.Domain.Abstractions;
using NotificationRouting.Domain.Recipients;
using System.Security.Cryptography;
using System.Text;

namespace NotificationRouting.Application;

public sealed class NotificationService : INotificationService
{
    private readonly IUserStore _users;
    private readonly ITopicStore _topics;
    private readonly IMessageArchive _archive;
    private readonly IAlertSink _alertSink;
    private readonly IDeliveryLog _deliveryLog;
    private readonly IDeliveryQueue _deliveryQueue;
    private readonly IDeliveryStore _deliveries;
    private readonly IIdempotencyStore _idempotency;
    private readonly IWebhookEndpointPolicy _webhookPolicy;
    private readonly IWebhookSink _webhookSink;

    public NotificationService(
        IUserStore users,
        ITopicStore topics,
        IMessageArchive archive,
        IAlertSink alertSink,
        IDeliveryLog deliveryLog,
        IDeliveryQueue deliveryQueue,
        IDeliveryStore deliveries,
        IIdempotencyStore idempotency,
        IWebhookEndpointPolicy webhookPolicy,
        IWebhookSink webhookSink)
    {
        _users = users;
        _topics = topics;
        _archive = archive;
        _alertSink = alertSink;
        _deliveryLog = deliveryLog;
        _deliveryQueue = deliveryQueue;
        _deliveries = deliveries;
        _idempotency = idempotency;
        _webhookPolicy = webhookPolicy;
        _webhookSink = webhookSink;
    }

    public User CreateUser(string name, IEnumerable<string>? alertKeywords, string? webhookEndpoint)
    {
        Uri? endpoint = string.IsNullOrWhiteSpace(webhookEndpoint)
            ? null
            : _webhookPolicy.Validate(webhookEndpoint);
        var user = new User(name, alertKeywords, endpoint);
        if (!_users.TryAdd(user))
            throw new NotificationConflictException($"A user named '{user.Name}' already exists.");

        return user;
    }

    public Topic CreateTopic(string name)
    {
        var topic = new Topic(name, new ArchiveRecipient(_archive));
        if (!_topics.TryAdd(topic))
            throw new NotificationConflictException($"A topic named '{topic.Name}' already exists.");

        return topic;
    }

    public void Subscribe(Guid topicId, Guid userId, Importance minimumImportance)
    {
        if (!Enum.IsDefined(minimumImportance))
            throw new ArgumentOutOfRangeException(nameof(minimumImportance));

        Topic topic = FindTopic(topicId);
        User user = FindUser(userId);
        var userRecipient = new UserRecipient(user);
        var alertRecipient = new KeywordAlertRecipient(user.Id, _alertSink, user.AlertKeywords);
        var group = new GroupRecipient([userRecipient, alertRecipient]);
        var logged = new LoggingRecipient(group, _deliveryLog, user.Name);
        var filtered = new ImportanceFilterRecipient(logged, minimumImportance);
        var targets = new List<RecipientRegistration>
        {
            new($"inbox:{user.Id}", filtered),
        };

        if (user.WebhookEndpoint is not null)
        {
            var webhook = new WebhookRecipient(user.Id, user.WebhookEndpoint, _webhookSink);
            var loggedWebhook = new LoggingRecipient(webhook, _deliveryLog, $"{user.Name} webhook");
            targets.Add(new RecipientRegistration(
                $"webhook:{user.Id}",
                new ImportanceFilterRecipient(loggedWebhook, minimumImportance)));
        }

        if (!topic.Subscribe(userId, targets))
            throw new NotificationConflictException("The user is already subscribed to this topic.");
    }

    public ValueTask<PublishReceipt> PublishAsync(
        Guid topicId,
        string title,
        string body,
        Importance importance,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        Topic topic = FindTopic(topicId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        string fingerprint = CreateFingerprint(topicId, title, body, importance);
        return _idempotency.ExecuteAsync(
            idempotencyKey,
            fingerprint,
            async operationCancellationToken =>
            {
                var message = new Message(title, body, importance);
                var dispatch = new DeliveryDispatch(topic.Id, message, topic.SnapshotRecipients());
                _deliveries.Add(dispatch);
                try
                {
                    await _deliveryQueue.EnqueueAsync(
                        new DeliveryCommand(dispatch),
                        operationCancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (operationCancellationToken.IsCancellationRequested)
                {
                    _deliveries.Remove(message.Id);
                    throw;
                }
                catch (Exception exception)
                {
                    _deliveries.Remove(message.Id);
                    throw new DeliveryQueueUnavailableException("Delivery queue is not accepting messages.", exception);
                }

                return new PublishReceipt(message.Id, false);
            },
            cancellationToken);
    }

    public DeliveryDispatchSnapshot GetDispatch(Guid messageId)
    {
        DeliveryDispatch? dispatch = _deliveries.Find(messageId);
        return dispatch?.Snapshot()
            ?? throw new NotificationNotFoundException($"Dispatch for message '{messageId}' was not found.");
    }

    public IReadOnlyList<DeadLetterEntry> GetDeadLetters()
    {
        return _deliveries.GetDeadLetters();
    }

    public async ValueTask<Guid> RetryAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        RetryPreparation preparation = _deliveries.PrepareRetry(deliveryId);
        if (preparation.Result == RetryPreparationResult.NotFound)
            throw new NotificationNotFoundException($"Delivery '{deliveryId}' was not found.");

        if (preparation.Result == RetryPreparationResult.Conflict || preparation.Command is null)
            throw new DeliveryStateConflictException("Only a dead-lettered delivery can be retried.");

        try
        {
            await _deliveryQueue.EnqueueAsync(preparation.Command, cancellationToken).ConfigureAwait(false);
            return preparation.Command.Dispatch.Message.Id;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _deliveries.RestoreDeadLetter(deliveryId);
            throw;
        }
        catch (Exception exception)
        {
            _deliveries.RestoreDeadLetter(deliveryId);
            throw new DeliveryQueueUnavailableException("Delivery queue is not accepting retries.", exception);
        }
    }

    public IReadOnlyList<UserMessageEntry> GetUserMessages(Guid userId)
    {
        return FindUser(userId).GetInbox();
    }

    public IReadOnlyList<Message> GetArchive()
    {
        return _archive.GetAll();
    }

    public OperationResult MarkRead(Guid userId, Guid messageId)
    {
        return FindUser(userId).MarkRead(messageId);
    }

    private static string CreateFingerprint(Guid topicId, string title, string body, Importance importance)
    {
        string input = string.Join('\u001f', topicId, title.Trim(), body, (int)importance);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }

    private User FindUser(Guid id)
    {
        return _users.Find(id) ?? throw new NotificationNotFoundException($"User '{id}' was not found.");
    }

    private Topic FindTopic(Guid id)
    {
        return _topics.Find(id) ?? throw new NotificationNotFoundException($"Topic '{id}' was not found.");
    }
}
