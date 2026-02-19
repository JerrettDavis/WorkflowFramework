namespace WorkflowFramework.Samples.TaskStream.Models;

/// <summary>
/// Represents a raw message from any input source.
/// </summary>
public sealed class SourceMessage
{
    /// <summary>Gets or sets the message identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>Gets or sets the source type (email, discord, webhook, file).</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Gets or sets the raw content.</summary>
    public string RawContent { get; set; } = string.Empty;

    /// <summary>Gets or sets when the message was received.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets additional metadata.</summary>
    public Dictionary<string, string> Metadata { get; set; } = [];
}
