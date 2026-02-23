namespace WorkflowFramework.Dashboard.Api.Models;

/// <summary>
/// Summary of a workflow run.
/// </summary>
public sealed class RunSummary
{
    public string RunId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string WorkflowName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Dictionary<string, string>? StepResults { get; set; }
    public string? Error { get; set; }
}
