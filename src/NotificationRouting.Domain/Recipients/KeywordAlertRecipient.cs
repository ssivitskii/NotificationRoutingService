using NotificationRouting.Domain.Abstractions;

namespace NotificationRouting.Domain.Recipients;

public sealed class KeywordAlertRecipient : IRecipient
{
    private readonly Guid _userId;
    private readonly IAlertSink _alertSink;
    private readonly IReadOnlyList<string> _keywords;

    public KeywordAlertRecipient(Guid userId, IAlertSink alertSink, IEnumerable<string> keywords)
    {
        _userId = userId;
        _alertSink = alertSink ?? throw new ArgumentNullException(nameof(alertSink));
        ArgumentNullException.ThrowIfNull(keywords);
        _keywords = keywords.Where(keyword => !string.IsNullOrWhiteSpace(keyword)).ToArray();
    }

    public ValueTask DeliverAsync(DeliveryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Message message = context.Message;
        string content = string.Concat(message.Title, "\n", message.Body);
        string? matched = _keywords.FirstOrDefault(
            keyword => content.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        if (matched is not null)
            _alertSink.Notify(_userId, message, matched);

        return ValueTask.CompletedTask;
    }
}
