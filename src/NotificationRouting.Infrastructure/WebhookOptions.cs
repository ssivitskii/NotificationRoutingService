namespace NotificationRouting.Infrastructure;

public sealed class WebhookOptions
{
    public const string SectionName = "Webhooks";

    public string[] AllowedHosts { get; set; } = [];

    public bool AllowHttp { get; set; }
}
