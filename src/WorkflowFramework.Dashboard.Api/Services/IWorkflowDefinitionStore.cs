using WorkflowFramework.Dashboard.Api.Models;

namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// Persistence store for workflow definitions used by the dashboard API.
/// </summary>
public interface IWorkflowDefinitionStore
{
    Task<IReadOnlyList<SavedWorkflowDefinition>> GetAllAsync(CancellationToken ct = default);
    Task<SavedWorkflowDefinition?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<SavedWorkflowDefinition> CreateAsync(CreateWorkflowRequest request, CancellationToken ct = default);
    Task<SavedWorkflowDefinition?> UpdateAsync(string id, CreateWorkflowRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
    Task<SavedWorkflowDefinition?> DuplicateAsync(string id, CancellationToken ct = default);
    Task SeedAsync(SavedWorkflowDefinition workflow, CancellationToken ct = default);
}
