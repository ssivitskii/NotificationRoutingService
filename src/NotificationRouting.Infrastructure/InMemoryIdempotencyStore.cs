using NotificationRouting.Application;
using NotificationRouting.Application.Abstractions;
using System.Collections.Concurrent;

namespace NotificationRouting.Infrastructure;

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public async ValueTask<PublishReceipt> ExecuteAsync(
        string key,
        string fingerprint,
        Func<CancellationToken, ValueTask<PublishReceipt>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);
        ArgumentNullException.ThrowIfNull(operation);

        var candidate = new Entry(fingerprint, () => operation(cancellationToken).AsTask());
        Entry entry = _entries.GetOrAdd(key, candidate);
        if (!string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal))
            throw new IdempotencyConflictException("The idempotency key was already used with a different request.");

        bool isReplay = !ReferenceEquals(entry, candidate);
        Task<PublishReceipt> operationTask = entry.Operation.Value;
        try
        {
            PublishReceipt receipt = isReplay
                ? await operationTask.WaitAsync(cancellationToken).ConfigureAwait(false)
                : await operationTask.ConfigureAwait(false);
            return receipt with { IsReplay = isReplay };
        }
        catch
        {
            if (operationTask.IsCanceled || operationTask.IsFaulted)
                _entries.TryRemove(new KeyValuePair<string, Entry>(key, entry));

            throw;
        }
    }

    private sealed record Entry(string Fingerprint, Lazy<Task<PublishReceipt>> Operation)
    {
        public Entry(string fingerprint, Func<Task<PublishReceipt>> operation)
            : this(fingerprint, new Lazy<Task<PublishReceipt>>(operation, LazyThreadSafetyMode.ExecutionAndPublication))
        {
        }
    }
}
