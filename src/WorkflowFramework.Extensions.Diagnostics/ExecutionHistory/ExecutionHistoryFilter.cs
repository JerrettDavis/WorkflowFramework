namespace WorkflowFramework.Extensions.Diagnostics.ExecutionHistory;

/// <summary>
/// Filter criteria for querying workflow run history.
/// </summary>
public sealed class ExecutionHistoryFilter
{
    /// <summary>Gets or sets an optional workflow name filter.</summary>
    public string? WorkflowName { get; set; }

    /// <summary>Gets or sets an optional status filter.</summary>
    public WorkflowStatus? Status { get; set; }

    /// <summary>Gets or sets the minimum start time.</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>Gets or sets the maximum start time.</summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>Gets or sets the maximum number of results to return.</summary>
    public int? MaxResults { get; set; }
}
