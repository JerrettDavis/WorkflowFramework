namespace WorkflowFramework.Persistence;

/// <summary>
/// Represents a serializable snapshot of workflow state for checkpointing.
/// </summary>
public sealed class WorkflowState
{
    /// <summary>
    /// Gets or sets the workflow identifier.
    /// </summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the correlation identifier.
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow name.
    /// </summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the index of the last completed step.
    /// </summary>
    public int LastCompletedStepIndex { get; set; } = -1;

    /// <summary>
    /// Gets or sets the workflow status.
    /// </summary>
    public WorkflowStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the serialized properties bag.
    /// </summary>
    public IDictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?>();

    /// <summary>
    /// Gets or sets the serialized workflow data (for typed workflows).
    /// </summary>
    public string? SerializedData { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this state was saved.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}
