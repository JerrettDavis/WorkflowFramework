namespace WorkflowFramework.Triggers;

/// <summary>
/// Serializable trigger configuration attached to a workflow.
/// </summary>
public sealed class TriggerDefinition
{
    /// <summary>Unique trigger instance ID.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Trigger type identifier — matches <see cref="ITriggerSource.Type"/>.</summary>
    public string Type { get; set; } = "";

    /// <summary>Optional display name for this trigger instance.</summary>
    public string? Name { get; set; }

    /// <summary>Whether the trigger is enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Trigger-specific configuration key-value pairs.</summary>
    public Dictionary<string, string> Configuration { get; set; } = new Dictionary<string, string>();
}
