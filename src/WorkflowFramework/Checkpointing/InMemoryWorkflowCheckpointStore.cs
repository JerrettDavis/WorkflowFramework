using System.Collections.Concurrent;

namespace WorkflowFramework.Checkpointing;

/// <summary>
/// In-memory implementation of <see cref="IWorkflowCheckpointStore"/>.
/// </summary>
public sealed class InMemoryWorkflowCheckpointStore : IWorkflowCheckpointStore
{
    private readonly ConcurrentDictionary<string, WorkflowCheckpoint> _checkpoints = new();

    /// <inheritdoc />
    public Task SaveAsync(string workflowId, int stepIndex, IDictionary<string, object?> contextSnapshot, CancellationToken cancellationToken = default)
    {
        var checkpoint = new WorkflowCheckpoint
        {
            WorkflowId = workflowId,
            StepIndex = stepIndex,
            ContextSnapshot = new Dictionary<string, object?>(contextSnapshot),
            Timestamp = DateTimeOffset.UtcNow
        };

        _checkpoints[workflowId] = checkpoint;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<WorkflowCheckpoint?> LoadAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        _checkpoints.TryGetValue(workflowId, out var checkpoint);
        return Task.FromResult(checkpoint);
    }

    /// <inheritdoc />
    public Task ClearAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        _checkpoints.TryRemove(workflowId, out _);
        return Task.CompletedTask;
    }
}
