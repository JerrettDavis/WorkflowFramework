namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// A snapshot of context state for checkpointing.
/// </summary>
public sealed class ContextSnapshot
{
    /// <summary>Gets or sets the messages.</summary>
    public IList<ConversationMessage> Messages { get; set; } = new List<ConversationMessage>();

    /// <summary>Gets or sets properties.</summary>
    public IDictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?>();

    /// <summary>Gets or sets the step name.</summary>
    public string? StepName { get; set; }

    /// <summary>Gets or sets the timestamp.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
