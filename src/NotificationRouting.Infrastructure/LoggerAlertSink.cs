using Microsoft.Extensions.Logging;
using NotificationRouting.Domain;
using NotificationRouting.Domain.Abstractions;

namespace NotificationRouting.Infrastructure;

public sealed class LoggerAlertSink : IAlertSink
{
    private readonly ILogger<LoggerAlertSink> _logger;

    public LoggerAlertSink(ILogger<LoggerAlertSink> logger)
    {
        _logger = logger;
    }

    public void Notify(Guid userId, Message message, string keyword)
    {
        _logger.LogWarning(
            "Keyword alert for user {UserId}: message {MessageId} matched {Keyword}",
            userId,
            message.Id,
            keyword);
    }
}
