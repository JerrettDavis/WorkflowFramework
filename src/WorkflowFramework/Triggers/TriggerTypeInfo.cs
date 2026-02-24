namespace WorkflowFramework.Triggers;

/// <summary>
/// Metadata about an available trigger type for the UI.
/// </summary>
public sealed class TriggerTypeInfo
{
    /// <summary>Trigger type identifier.</summary>
    public string Type { get; set; } = "";

    /// <summary>Human-readable display name.</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>Description for the UI.</summary>
    public string Description { get; set; } = "";

    /// <summary>Category grouping (e.g., "Time", "File", "Messaging").</summary>
    public string Category { get; set; } = "";

    /// <summary>Optional JSON schema describing the configuration fields.</summary>
    public string? ConfigSchema { get; set; }

    /// <summary>Optional icon identifier for the UI.</summary>
    public string? Icon { get; set; }
}
