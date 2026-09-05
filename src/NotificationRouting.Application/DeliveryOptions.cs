namespace NotificationRouting.Application;

public sealed class DeliveryOptions
{
    public const string SectionName = "Delivery";

    public int ChannelCapacity { get; set; } = 256;

    public int MaxAutomaticAttempts { get; set; } = 3;

    public int BaseRetryDelayMilliseconds { get; set; } = 250;

    public int WebhookTimeoutSeconds { get; set; } = 5;
}
