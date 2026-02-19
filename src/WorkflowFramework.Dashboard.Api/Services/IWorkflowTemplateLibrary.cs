using WorkflowFramework.Dashboard.Api.Models;

namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// Provides access to pre-built workflow templates.
/// </summary>
public interface IWorkflowTemplateLibrary
{
    /// <summary>Gets all templates, optionally filtered by category and/or tag.</summary>
    Task<IReadOnlyList<WorkflowTemplateSummary>> GetTemplatesAsync(string? category = null, string? tag = null, CancellationToken ct = default);

    /// <summary>Gets a single template by ID including its full definition.</summary>
    Task<WorkflowTemplate?> GetTemplateAsync(string id, CancellationToken ct = default);

    /// <summary>Gets all available categories.</summary>
    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken ct = default);
}
