namespace WorkflowFramework.Dashboard.Services;

/// <summary>
/// Summary information about a registered workflow.
/// </summary>
public sealed class WorkflowSummary
{
    /// <summary>Gets or sets the workflow name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of steps.</summary>
    public int StepCount { get; set; }

    /// <summary>Gets or sets the status of the last run, if any.</summary>
    public WorkflowStatus? LastRunStatus { get; set; }

    /// <summary>Gets or sets when the last run started, if any.</summary>
    public DateTimeOffset? LastRunAt { get; set; }
}
