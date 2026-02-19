using System.Collections.Concurrent;
using System.Text.Json;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Serialization;

namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// Manages workflow version history with in-memory storage.
/// </summary>
public sealed class WorkflowVersioningService
{
    private readonly ConcurrentDictionary<string, List<WorkflowVersion>> _versions = new();

    /// <summary>
    /// Creates a new version snapshot for a workflow.
    /// </summary>
    public WorkflowVersion CreateVersion(SavedWorkflowDefinition workflow, string author = "system", string changeSummary = "")
    {
        var versions = _versions.GetOrAdd(workflow.Id, _ => []);
        lock (versions)
        {
            var versionNumber = versions.Count + 1;
            var snapshot = DeepClone(workflow);
            var version = new WorkflowVersion
            {
                VersionNumber = versionNumber,
                Timestamp = DateTimeOffset.UtcNow,
                Author = author,
                ChangeSummary = changeSummary.Length > 0 ? changeSummary : GenerateChangeSummary(versions, snapshot),
                Snapshot = snapshot
            };
            versions.Add(version);
            return version;
        }
    }

    /// <summary>
    /// Gets all versions for a workflow.
    /// </summary>
    public IReadOnlyList<WorkflowVersion> GetVersions(string workflowId)
    {
        return _versions.TryGetValue(workflowId, out var versions)
            ? versions.AsReadOnly()
            : [];
    }

    /// <summary>
    /// Gets a specific version of a workflow.
    /// </summary>
    public WorkflowVersion? GetVersion(string workflowId, int versionNumber)
    {
        if (!_versions.TryGetValue(workflowId, out var versions))
            return null;
        lock (versions)
        {
            return versions.FirstOrDefault(v => v.VersionNumber == versionNumber);
        }
    }

    /// <summary>
    /// Computes a diff between two versions.
    /// </summary>
    public WorkflowVersionDiff? Diff(string workflowId, int fromVersion, int toVersion)
    {
        var from = GetVersion(workflowId, fromVersion);
        var to = GetVersion(workflowId, toVersion);
        if (from is null || to is null) return null;

        var fromSteps = from.Snapshot.Definition.Steps.ToDictionary(s => s.Name);
        var toSteps = to.Snapshot.Definition.Steps.ToDictionary(s => s.Name);

        var diff = new WorkflowVersionDiff
        {
            FromVersion = fromVersion,
            ToVersion = toVersion
        };

        // Added
        foreach (var (name, step) in toSteps)
        {
            if (!fromSteps.ContainsKey(name))
                diff.AddedSteps.Add(new StepChange { Name = step.Name, Type = step.Type });
        }

        // Removed
        foreach (var (name, step) in fromSteps)
        {
            if (!toSteps.ContainsKey(name))
                diff.RemovedSteps.Add(new StepChange { Name = step.Name, Type = step.Type });
        }

        // Modified
        foreach (var (name, fromStep) in fromSteps)
        {
            if (toSteps.TryGetValue(name, out var toStep) && fromStep.Type != toStep.Type)
            {
                diff.ModifiedSteps.Add(new StepModification
                {
                    Name = name,
                    Type = toStep.Type,
                    Field = "Type",
                    OldValue = fromStep.Type,
                    NewValue = toStep.Type
                });
            }
        }

        // Name change
        if (from.Snapshot.Definition.Name != to.Snapshot.Definition.Name)
        {
            diff.NameChanged = true;
            diff.OldName = from.Snapshot.Definition.Name;
            diff.NewName = to.Snapshot.Definition.Name;
        }

        return diff;
    }

    private static string GenerateChangeSummary(List<WorkflowVersion> versions, SavedWorkflowDefinition current)
    {
        if (versions.Count == 0) return "Initial version";
        var prev = versions[^1].Snapshot;
        var changes = new List<string>();
        if (prev.Definition.Name != current.Definition.Name)
            changes.Add($"Renamed from '{prev.Definition.Name}' to '{current.Definition.Name}'");
        var prevCount = prev.Definition.Steps.Count;
        var curCount = current.Definition.Steps.Count;
        if (curCount > prevCount) changes.Add($"Added {curCount - prevCount} step(s)");
        else if (curCount < prevCount) changes.Add($"Removed {prevCount - curCount} step(s)");
        return changes.Count > 0 ? string.Join("; ", changes) : "Updated";
    }

    private static SavedWorkflowDefinition DeepClone(SavedWorkflowDefinition src)
    {
        var json = JsonSerializer.Serialize(src);
        return JsonSerializer.Deserialize<SavedWorkflowDefinition>(json)!;
    }
}
