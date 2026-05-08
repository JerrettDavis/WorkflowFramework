namespace WorkflowFramework.Dashboard.Persistence.Entities;

/// <summary>
/// Persisted representation of a directed edge between two history graph nodes.
/// </summary>
public sealed class HistoryEdgeEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SourceFingerprint { get; set; } = string.Empty;
    public string TargetFingerprint { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public long Weight { get; set; }
    public long AverageTransitionTimeTicks { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public string WorkflowNamesJson { get; set; } = "[]";
    public HistoryNodeEntity? SourceNode { get; set; }
    public HistoryNodeEntity? TargetNode { get; set; }
}
