namespace WorkflowFramework.Checkpointing;

/// <summary>
/// Represents a checkpoint snapshot for resuming a workflow from a specific step.
/// </summary>
public sealed class WorkflowCheckpoint
{
    /// <summary>
    /// Gets or sets the workflow identifier.
    /// </summary>
    public required string WorkflowId { get; set; }

    /// <summary>
    /// Gets or sets the index of the last successfully completed step.
    /// </summary>
    public required int StepIndex { get; set; }

    /// <summary>
    /// Gets or sets the name of the last successfully completed step.
    /// </summary>
    public string? StepName { get; set; }

    /// <summary>
    /// Gets or sets the snapshot of context properties at the checkpoint.
    /// </summary>
    public required IDictionary<string, object?> ContextSnapshot { get; set; }

    /// <summary>
    /// Gets or sets the name of the step that failed, if any.
    /// </summary>
    public string? FailedStepName { get; set; }

    /// <summary>
    /// Gets or sets the index of the step that failed, if any.
    /// </summary>
    public int? FailedStepIndex { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this checkpoint was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Store for saving and loading workflow checkpoints to enable resume from failure.
/// </summary>
public interface IWorkflowCheckpointStore
{
    /// <summary>
    /// Saves a checkpoint after a successful step.
    /// </summary>
    Task SaveAsync(string workflowId, int stepIndex, IDictionary<string, object?> contextSnapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the last checkpoint for a workflow.
    /// </summary>
    Task<WorkflowCheckpoint?> LoadAsync(string workflowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all checkpoints for a workflow.
    /// </summary>
    Task ClearAsync(string workflowId, CancellationToken cancellationToken = default);
}
