namespace WorkflowFramework.Dashboard.Api.Models;

/// <summary>
/// A snapshot of a workflow definition at a specific version.
/// </summary>
public sealed class WorkflowVersion
{
    public int VersionNumber { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string Author { get; set; } = "system";
    public string ChangeSummary { get; set; } = "";
    public SavedWorkflowDefinition Snapshot { get; set; } = new();
}

/// <summary>
/// Diff result between two workflow versions.
/// </summary>
public sealed class WorkflowVersionDiff
{
    public int FromVersion { get; set; }
    public int ToVersion { get; set; }
    public List<StepChange> AddedSteps { get; set; } = [];
    public List<StepChange> RemovedSteps { get; set; } = [];
    public List<StepModification> ModifiedSteps { get; set; } = [];
    public bool NameChanged { get; set; }
    public string? OldName { get; set; }
    public string? NewName { get; set; }
}

public sealed class StepChange
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
}

public sealed class StepModification
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Field { get; set; } = "";
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
}
