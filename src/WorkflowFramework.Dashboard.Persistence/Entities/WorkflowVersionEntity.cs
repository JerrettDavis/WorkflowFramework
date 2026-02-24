namespace WorkflowFramework.Dashboard.Persistence.Entities;

/// <summary>
/// A versioned snapshot of a workflow definition.
/// </summary>
public sealed class WorkflowVersionEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string WorkflowId { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string DefinitionJson { get; set; } = "{}";
    public string? ChangeSummary { get; set; }
    public string? Author { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public WorkflowEntity? Workflow { get; set; }
}
