namespace WorkflowFramework.Dashboard.Api.Models;

/// <summary>
/// Information about a loaded plugin.
/// </summary>
public sealed class PluginInfo
{
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Information about an available connector.
/// </summary>
public sealed class ConnectorInfo
{
    public string Name { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? Description { get; set; }
}
