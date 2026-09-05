namespace NotificationRouting.Application;

public sealed class IdempotencyConflictException : Exception
{
    public IdempotencyConflictException(string message)
        : base(message)
    {
    }
}
