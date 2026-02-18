namespace WorkflowFramework.Extensions.Distributed;

/// <summary>
/// Abstraction for queuing workflow executions.
/// </summary>
public interface IWorkflowQueue
{
    /// <summary>Enqueues a workflow for execution.</summary>
    Task EnqueueAsync(WorkflowQueueItem item, CancellationToken cancellationToken = default);

    /// <summary>Dequeues the next workflow for execution.</summary>
    Task<WorkflowQueueItem?> DequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the current queue length.</summary>
    Task<int> GetLengthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a queued workflow execution request.
/// </summary>
public sealed class WorkflowQueueItem
{
    /// <summary>Gets or sets the unique item identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the workflow name.</summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>Gets or sets the serialized context data.</summary>
    public string? SerializedData { get; set; }

    /// <summary>Gets or sets when the item was enqueued.</summary>
    public DateTimeOffset EnqueuedAt { get; set; } = DateTimeOffset.UtcNow;
}
