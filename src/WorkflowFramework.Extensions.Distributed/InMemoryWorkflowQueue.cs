using System.Collections.Concurrent;

namespace WorkflowFramework.Extensions.Distributed;

/// <summary>
/// In-memory implementation of <see cref="IWorkflowQueue"/>.
/// </summary>
public sealed class InMemoryWorkflowQueue : IWorkflowQueue
{
    private readonly ConcurrentQueue<WorkflowQueueItem> _queue = new();

    /// <inheritdoc />
    public Task EnqueueAsync(WorkflowQueueItem item, CancellationToken cancellationToken = default)
    {
        _queue.Enqueue(item ?? throw new ArgumentNullException(nameof(item)));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<WorkflowQueueItem?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        _queue.TryDequeue(out var item);
        return Task.FromResult(item);
    }

    /// <inheritdoc />
    public Task<int> GetLengthAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_queue.Count);
}
