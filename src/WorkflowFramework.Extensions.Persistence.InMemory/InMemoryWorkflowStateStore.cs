using System.Collections.Concurrent;
using WorkflowFramework.Persistence;

namespace WorkflowFramework.Extensions.Persistence.InMemory;

/// <summary>
/// In-memory implementation of <see cref="IWorkflowStateStore"/>.
/// Useful for testing and development.
/// </summary>
public sealed class InMemoryWorkflowStateStore : IWorkflowStateStore
{
    private readonly ConcurrentDictionary<string, WorkflowState> _states = new();

    /// <inheritdoc />
    public Task SaveCheckpointAsync(string workflowId, WorkflowState state, CancellationToken cancellationToken = default)
    {
        _states[workflowId] = state ?? throw new ArgumentNullException(nameof(state));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<WorkflowState?> LoadCheckpointAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        _states.TryGetValue(workflowId, out var state);
        return Task.FromResult(state);
    }

    /// <inheritdoc />
    public Task DeleteCheckpointAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        _states.TryRemove(workflowId, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets all stored states. Useful for testing.
    /// </summary>
    /// <returns>All stored workflow states.</returns>
    public IReadOnlyDictionary<string, WorkflowState> GetAllStates() => _states;
}
