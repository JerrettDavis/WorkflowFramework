using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Services;
using WorkflowFramework.Dashboard.Persistence;
using WorkflowFramework.Dashboard.Persistence.Entities;

namespace WorkflowFramework.Dashboard.Api.Persistence;

/// <summary>
/// EF Core workflow versioning store replacing the in-memory ConcurrentDictionary implementation.
/// </summary>
public sealed class EfWorkflowVersioningStore : IWorkflowVersioningService
{
    private readonly DashboardDbContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public EfWorkflowVersioningStore(DashboardDbContext db) => _db = db;

    public WorkflowVersion CreateVersion(SavedWorkflowDefinition workflow, string author = "system", string changeSummary = "")
    {
        var versions = _db.WorkflowVersions
            .Where(v => v.WorkflowId == workflow.Id)
            .OrderByDescending(v => v.VersionNumber)
            .ToList();

        var versionNumber = versions.Count > 0 ? versions[0].VersionNumber + 1 : 1;

        if (string.IsNullOrEmpty(changeSummary))
            changeSummary = GenerateChangeSummary(versions, workflow);

        var entity = new WorkflowVersionEntity
        {
            WorkflowId = workflow.Id,
            VersionNumber = versionNumber,
            DefinitionJson = JsonSerializer.Serialize(workflow, JsonOptions),
            Author = author,
            ChangeSummary = changeSummary,
            Timestamp = DateTimeOffset.UtcNow
        };

        _db.WorkflowVersions.Add(entity);
        _db.SaveChanges();

        return ToModel(entity);
    }

    public IReadOnlyList<WorkflowVersion> GetVersions(string workflowId)
    {
        return _db.WorkflowVersions
            .Where(v => v.WorkflowId == workflowId)
            .OrderBy(v => v.VersionNumber)
            .AsEnumerable()
            .Select(ToModel)
            .ToList();
    }

    public WorkflowVersion? GetVersion(string workflowId, int versionNumber)
    {
        var entity = _db.WorkflowVersions
            .FirstOrDefault(v => v.WorkflowId == workflowId && v.VersionNumber == versionNumber);
        return entity is null ? null : ToModel(entity);
    }

    public WorkflowVersionDiff? Diff(string workflowId, int fromVersion, int toVersion)
    {
        var from = GetVersion(workflowId, fromVersion);
        var to = GetVersion(workflowId, toVersion);
        if (from is null || to is null) return null;

        var fromSteps = from.Snapshot.Definition.Steps.ToDictionary(s => s.Name);
        var toSteps = to.Snapshot.Definition.Steps.ToDictionary(s => s.Name);

        var diff = new WorkflowVersionDiff { FromVersion = fromVersion, ToVersion = toVersion };

        foreach (var (name, step) in toSteps)
            if (!fromSteps.ContainsKey(name))
                diff.AddedSteps.Add(new StepChange { Name = step.Name, Type = step.Type });

        foreach (var (name, step) in fromSteps)
            if (!toSteps.ContainsKey(name))
                diff.RemovedSteps.Add(new StepChange { Name = step.Name, Type = step.Type });

        foreach (var (name, fromStep) in fromSteps)
            if (toSteps.TryGetValue(name, out var toStep) && fromStep.Type != toStep.Type)
                diff.ModifiedSteps.Add(new StepModification
                {
                    Name = name, Type = toStep.Type, Field = "Type",
                    OldValue = fromStep.Type, NewValue = toStep.Type
                });

        if (from.Snapshot.Definition.Name != to.Snapshot.Definition.Name)
        {
            diff.NameChanged = true;
            diff.OldName = from.Snapshot.Definition.Name;
            diff.NewName = to.Snapshot.Definition.Name;
        }

        return diff;
    }

    private static string GenerateChangeSummary(List<WorkflowVersionEntity> versions, SavedWorkflowDefinition current)
    {
        if (versions.Count == 0) return "Initial version";
        var prev = JsonSerializer.Deserialize<SavedWorkflowDefinition>(versions[0].DefinitionJson, JsonOptions);
        if (prev is null) return "Updated";

        var changes = new List<string>();
        if (prev.Definition.Name != current.Definition.Name)
            changes.Add($"Renamed from '{prev.Definition.Name}' to '{current.Definition.Name}'");
        var prevCount = prev.Definition.Steps.Count;
        var curCount = current.Definition.Steps.Count;
        if (curCount > prevCount) changes.Add($"Added {curCount - prevCount} step(s)");
        else if (curCount < prevCount) changes.Add($"Removed {prevCount - curCount} step(s)");
        return changes.Count > 0 ? string.Join("; ", changes) : "Updated";
    }

    private static WorkflowVersion ToModel(WorkflowVersionEntity entity) => new()
    {
        VersionNumber = entity.VersionNumber,
        Timestamp = entity.Timestamp,
        Author = entity.Author ?? "system",
        ChangeSummary = entity.ChangeSummary ?? "",
        Snapshot = JsonSerializer.Deserialize<SavedWorkflowDefinition>(entity.DefinitionJson, JsonOptions)
                   ?? new SavedWorkflowDefinition()
    };
}
