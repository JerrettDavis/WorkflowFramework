namespace WorkflowFramework.Triggers;

/// <summary>
/// Context provided to a trigger source, including the callback to fire when triggered.
/// </summary>
public sealed class TriggerContext
{
    /// <summary>The workflow ID this trigger is bound to.</summary>
    public string WorkflowId { get; set; } = "";

    /// <summary>Trigger-specific configuration (key-value pairs from the UI).</summary>
    public IReadOnlyDictionary<string, string> Configuration { get; set; } = new Dictionary<string, string>();

    /// <summary>Callback to invoke when the trigger fires. Returns the run ID.</summary>
    public Func<TriggerEvent, Task<string>> OnTriggered { get; set; } = null!;
}
