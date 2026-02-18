namespace WorkflowFramework.Extensions.Distributed;

/// <summary>
/// Abstraction for distributed locking.
/// </summary>
public interface IDistributedLock
{
    /// <summary>
    /// Acquires a lock with the given key.
    /// </summary>
    /// <param name="key">The lock key.</param>
    /// <param name="expiry">The lock expiry duration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A disposable lock handle, or null if the lock could not be acquired.</returns>
    Task<IAsyncDisposable?> AcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);
}
