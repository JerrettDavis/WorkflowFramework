namespace WorkflowFramework.Dashboard.Persistence.Entities;

/// <summary>
/// Granular per-step execution history within a workflow run.
/// </summary>
public sealed class StepRunEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string RunId { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public int StepIndex { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Output { get; set; }
    public string? Error { get; set; }
    public long DurationMs { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    // Navigation
    public WorkflowRunEntity? Run { get; set; }
}
