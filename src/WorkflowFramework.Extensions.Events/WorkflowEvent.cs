namespace WorkflowFramework.Extensions.Events;

/// <summary>
/// Represents a workflow event.
/// </summary>
public sealed class WorkflowEvent
{
    /// <summary>Gets or sets the unique event ID.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the event type.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Gets or sets the correlation ID for matching events to workflows.</summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>Gets or sets the event payload.</summary>
    public IDictionary<string, object?> Payload { get; set; } = new Dictionary<string, object?>();

    /// <summary>Gets or sets when this event occurred.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the source of this event.</summary>
    public string? Source { get; set; }
}
