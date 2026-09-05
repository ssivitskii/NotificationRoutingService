using NotificationRouting.Domain;
using NotificationRouting.Domain.Abstractions;

namespace NotificationRouting.Infrastructure;

public sealed class MarkdownMessageFormatter : IMessageFormatter
{
    public string Format(Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return $"# {message.Title}{Environment.NewLine}> Importance: **{message.Importance}**{Environment.NewLine}{Environment.NewLine}{message.Body}";
    }
}
