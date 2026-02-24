using WorkflowFramework.Dashboard.Api.Models;

namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// Manages workflow version history.
/// </summary>
public interface IWorkflowVersioningService
{
    WorkflowVersion CreateVersion(SavedWorkflowDefinition workflow, string author = "system", string changeSummary = "");
    IReadOnlyList<WorkflowVersion> GetVersions(string workflowId);
    WorkflowVersion? GetVersion(string workflowId, int versionNumber);
    WorkflowVersionDiff? Diff(string workflowId, int fromVersion, int toVersion);
}
