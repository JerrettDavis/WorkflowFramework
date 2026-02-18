using System.Collections.Concurrent;

namespace WorkflowFramework.Extensions.Distributed;

/// <summary>
/// In-memory implementation of <see cref="IDistributedLock"/> for testing.
/// </summary>
public sealed class InMemoryDistributedLock : IDistributedLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    /// <inheritdoc />
    public async Task<IAsyncDisposable?> AcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        if (await semaphore.WaitAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false))
        {
            return new LockHandle(semaphore);
        }
        return null;
    }

    private sealed class LockHandle : IAsyncDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private int _disposed;

        public LockHandle(SemaphoreSlim semaphore) => _semaphore = semaphore;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
                _semaphore.Release();
            return default;
        }
    }
}
