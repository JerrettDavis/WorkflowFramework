namespace WorkflowFramework.Extensions.Diagnostics.ExecutionHistory;

/// <summary>
/// Records the execution details of a single workflow step.
/// </summary>
public sealed class StepRunRecord
{
    /// <summary>Gets or sets the step name.</summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>Gets or sets the execution status.</summary>
    public WorkflowStatus Status { get; set; }

    /// <summary>Gets or sets when the step started.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Gets or sets when the step completed.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Gets the duration of step execution.</summary>
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;

    /// <summary>Gets or sets the error message if the step failed.</summary>
    public string? Error { get; set; }

    /// <summary>Gets or sets a snapshot of the input data before the step ran.</summary>
    public IDictionary<string, object?>? InputSnapshot { get; set; }

    /// <summary>Gets or sets a snapshot of the output data after the step ran.</summary>
    public IDictionary<string, object?>? OutputSnapshot { get; set; }
}
