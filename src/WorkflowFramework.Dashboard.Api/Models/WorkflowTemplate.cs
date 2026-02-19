using WorkflowFramework.Serialization;

namespace WorkflowFramework.Dashboard.Api.Models;

/// <summary>
/// Difficulty level for a workflow template.
/// </summary>
public enum TemplateDifficulty
{
    Beginner,
    Intermediate,
    Advanced
}

/// <summary>
/// A pre-built workflow template that users can browse and load into the designer.
/// </summary>
public sealed class WorkflowTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public TemplateDifficulty Difficulty { get; set; }
    public int StepCount { get; set; }
    public string? PreviewImageUrl { get; set; }
    public WorkflowDefinitionDto Definition { get; set; } = new();
}

/// <summary>
/// Summary of a template for list views (excludes the full definition).
/// </summary>
public sealed class WorkflowTemplateSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public TemplateDifficulty Difficulty { get; set; }
    public int StepCount { get; set; }
    public string? PreviewImageUrl { get; set; }
}
