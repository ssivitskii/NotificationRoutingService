namespace NotificationRouting.Application.Abstractions;

public interface IIdempotencyStore
{
    ValueTask<PublishReceipt> ExecuteAsync(
        string key,
        string fingerprint,
        Func<CancellationToken, ValueTask<PublishReceipt>> operation,
        CancellationToken cancellationToken);
}
