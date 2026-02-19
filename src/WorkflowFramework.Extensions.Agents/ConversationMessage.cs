namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Represents a message in the conversation history.
/// </summary>
public sealed class ConversationMessage
{
    /// <summary>Gets or sets the role.</summary>
    public ConversationRole Role { get; set; }

    /// <summary>Gets or sets the content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets the timestamp.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets arbitrary metadata.</summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

    /// <summary>Gets or sets whether this message has been compacted.</summary>
    public bool IsCompacted { get; set; }
}
