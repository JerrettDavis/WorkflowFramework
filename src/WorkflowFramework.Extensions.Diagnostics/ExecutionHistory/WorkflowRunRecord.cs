namespace WorkflowFramework.Extensions.Diagnostics.ExecutionHistory;

/// <summary>
/// Records the execution details of a complete workflow run.
/// </summary>
public sealed class WorkflowRunRecord
{
    /// <summary>Gets or sets the unique run identifier.</summary>
    public string RunId { get; set; } = string.Empty;

    /// <summary>Gets or sets the workflow name.</summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>Gets or sets the final workflow status.</summary>
    public WorkflowStatus Status { get; set; }

    /// <summary>Gets or sets when the workflow started.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Gets or sets when the workflow completed.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Gets the total duration of the workflow run.</summary>
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;

    /// <summary>Gets or sets the error message if the workflow failed.</summary>
    public string? Error { get; set; }

    /// <summary>Gets the step execution records.</summary>
    public List<StepRunRecord> StepResults { get; set; } = new();
}
