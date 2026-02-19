using System.Text.Json;

namespace WorkflowFramework.Dashboard.Api.Models;

/// <summary>
/// Metadata about an available step type.
/// </summary>
public sealed class StepTypeInfo
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public JsonElement? ConfigSchema { get; set; }
}
