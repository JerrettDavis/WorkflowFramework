namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Store for saving and loading context checkpoints.
/// </summary>
public interface ICheckpointStore
{
    /// <summary>Saves a checkpoint.</summary>
    Task SaveAsync(string workflowId, string checkpointId, ContextSnapshot snapshot, CancellationToken ct = default);

    /// <summary>Loads a checkpoint. Returns null if not found.</summary>
    Task<ContextSnapshot?> LoadAsync(string workflowId, string checkpointId, CancellationToken ct = default);

    /// <summary>Lists checkpoints for a workflow.</summary>
    Task<IReadOnlyList<CheckpointInfo>> ListAsync(string workflowId, CancellationToken ct = default);

    /// <summary>Deletes a checkpoint.</summary>
    Task DeleteAsync(string workflowId, string checkpointId, CancellationToken ct = default);
}
