namespace WorkflowFramework.Dashboard.Persistence.Entities;

/// <summary>
/// Persisted representation of a workflow history graph node (a step archetype).
/// </summary>
public sealed class HistoryNodeEntity
{
    public string Fingerprint { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? Target { get; set; }
    public long ExecutionCount { get; set; }
    public long SuccessCount { get; set; }
    public long FailureCount { get; set; }
    public long AverageDurationTicks { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public string WorkflowNamesJson { get; set; } = "[]";
    public List<HistoryEdgeEntity> OutgoingEdges { get; set; } = [];
    public List<HistoryEdgeEntity> IncomingEdges { get; set; } = [];
}
