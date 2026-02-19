using WorkflowFramework.Serialization;

namespace WorkflowFramework.Dashboard.Api.Models;

/// <summary>
/// A persisted workflow definition with metadata.
/// </summary>
public sealed class SavedWorkflowDefinition
{
    public string Id { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
    public DateTimeOffset LastModified { get; set; }
    public WorkflowDefinitionDto Definition { get; set; } = new();
}
