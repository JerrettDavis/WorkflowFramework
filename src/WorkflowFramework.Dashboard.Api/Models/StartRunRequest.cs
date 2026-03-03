using System.Text.Json;

namespace WorkflowFramework.Dashboard.Api.Models;

/// <summary>
/// Optional payload when starting a workflow run from the dashboard.
/// </summary>
public sealed class StartRunRequest
{
    public Dictionary<string, JsonElement>? Inputs { get; set; }
    public string? Source { get; set; }
}
