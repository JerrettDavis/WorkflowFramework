namespace WorkflowFramework.Extensions.Persistence.EntityFramework;

/// <summary>
/// Entity representing a persisted workflow state.
/// </summary>
public sealed class WorkflowStateEntity
{
    /// <summary>Gets or sets the workflow identifier (primary key).</summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>Gets or sets the correlation identifier.</summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>Gets or sets the workflow name.</summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>Gets or sets the last completed step index.</summary>
    public int LastCompletedStepIndex { get; set; } = -1;

    /// <summary>Gets or sets the workflow status.</summary>
    public int Status { get; set; }

    /// <summary>Gets or sets the serialized properties JSON.</summary>
    public string? PropertiesJson { get; set; }

    /// <summary>Gets or sets the serialized workflow data.</summary>
    public string? SerializedData { get; set; }

    /// <summary>Gets or sets the timestamp.</summary>
    public DateTimeOffset Timestamp { get; set; }
}
