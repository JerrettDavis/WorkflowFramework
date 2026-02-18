namespace WorkflowFramework.Persistence;

/// <summary>
/// Abstraction for persisting workflow state (checkpointing).
/// </summary>
public interface IWorkflowStateStore
{
    /// <summary>
    /// Saves a checkpoint for the given workflow.
    /// </summary>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="state">The state to persist.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SaveCheckpointAsync(string workflowId, WorkflowState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the checkpoint for the given workflow.
    /// </summary>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The workflow state, or null if no checkpoint exists.</returns>
    Task<WorkflowState?> LoadCheckpointAsync(string workflowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the checkpoint for the given workflow.
    /// </summary>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteCheckpointAsync(string workflowId, CancellationToken cancellationToken = default);
}
