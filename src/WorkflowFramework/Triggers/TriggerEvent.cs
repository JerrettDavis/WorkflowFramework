namespace WorkflowFramework.Triggers;

/// <summary>
/// Data passed when a trigger fires.
/// </summary>
public sealed class TriggerEvent
{
    /// <summary>The trigger type that fired.</summary>
    public string TriggerType { get; set; } = "";

    /// <summary>Timestamp of the trigger.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Payload data to inject into workflow context properties.</summary>
    public Dictionary<string, object> Payload { get; set; } = new Dictionary<string, object>();

    /// <summary>Optional correlation ID for tracing.</summary>
    public string? CorrelationId { get; set; }
}
