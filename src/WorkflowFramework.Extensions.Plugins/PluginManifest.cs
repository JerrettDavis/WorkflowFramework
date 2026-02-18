namespace WorkflowFramework.Extensions.Plugins;

/// <summary>
/// Describes a plugin's metadata and capabilities.
/// </summary>
public sealed class PluginManifest
{
    /// <summary>Gets or sets the plugin name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the plugin version.</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>Gets or sets the plugin description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the plugin author.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Gets or sets the capabilities this plugin provides.</summary>
    public IList<string> Capabilities { get; set; } = new List<string>();

    /// <summary>Gets or sets the plugin dependencies.</summary>
    public IList<string> Dependencies { get; set; } = new List<string>();
}
