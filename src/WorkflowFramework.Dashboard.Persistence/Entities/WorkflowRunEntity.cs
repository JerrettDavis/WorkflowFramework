namespace WorkflowFramework.Dashboard.Persistence.Entities;

/// <summary>
/// Persisted record of a workflow execution run.
/// </summary>
public sealed class WorkflowRunEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string WorkflowId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? Error { get; set; }
    public string? StepResultsJson { get; set; }
    public long? DurationMs { get; set; }
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    // Navigation
    public WorkflowEntity? Workflow { get; set; }
    public DashboardUser? User { get; set; }
    public List<StepRunEntity> StepRuns { get; set; } = [];
}
