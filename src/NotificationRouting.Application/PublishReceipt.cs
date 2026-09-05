namespace NotificationRouting.Application;

public sealed record PublishReceipt(Guid MessageId, bool IsReplay);
