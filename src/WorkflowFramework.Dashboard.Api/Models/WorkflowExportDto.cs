using WorkflowFramework.Serialization;

namespace WorkflowFramework.Dashboard.Api.Models;

/// <summary>
/// DTO for exporting/importing workflow definitions.
/// </summary>
public sealed class WorkflowExportDto
{
    public string FormatVersion { get; set; } = "1.0";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = [];
    public WorkflowDefinitionDto Definition { get; set; } = new();
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.UtcNow;
}
