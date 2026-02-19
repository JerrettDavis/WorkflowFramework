namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Result of a compaction operation.
/// </summary>
public sealed class CompactionResult
{
    /// <summary>Gets or sets the original message count.</summary>
    public int OriginalMessageCount { get; set; }

    /// <summary>Gets or sets the compacted message count.</summary>
    public int CompactedMessageCount { get; set; }

    /// <summary>Gets or sets the original estimated token count.</summary>
    public int OriginalTokenEstimate { get; set; }

    /// <summary>Gets or sets the compacted estimated token count.</summary>
    public int CompactedTokenEstimate { get; set; }

    /// <summary>Gets or sets the summary of compacted content.</summary>
    public string? Summary { get; set; }
}
