using Microsoft.Extensions.Logging;
using NotificationRouting.Domain;
using NotificationRouting.Domain.Abstractions;

namespace NotificationRouting.Infrastructure;

public sealed class LoggerDeliveryLog : IDeliveryLog
{
    private readonly ILogger<LoggerDeliveryLog> _logger;

    public LoggerDeliveryLog(ILogger<LoggerDeliveryLog> logger)
    {
        _logger = logger;
    }

    public void Delivered(Message message, string recipientName)
    {
        _logger.LogInformation(
            "Delivering message {MessageId} with importance {Importance} to {Recipient}",
            message.Id,
            message.Importance,
            recipientName);
    }
}
