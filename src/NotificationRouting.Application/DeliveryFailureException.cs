namespace NotificationRouting.Application;

public sealed class DeliveryFailureException : Exception
{
    public DeliveryFailureException(string message, bool retryable, int? httpStatusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        Retryable = retryable;
        HttpStatusCode = httpStatusCode;
    }

    public bool Retryable { get; }

    public int? HttpStatusCode { get; }
}
