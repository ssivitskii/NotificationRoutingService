namespace NotificationRouting.Application;

public sealed record DeliveryCommand(DeliveryDispatch Dispatch, Guid? TargetId = null);
