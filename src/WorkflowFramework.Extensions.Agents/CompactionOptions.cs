namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Options for context compaction.
/// </summary>
public sealed class CompactionOptions
{
    /// <summary>Gets or sets the max token threshold that triggers compaction.</summary>
    public int MaxTokens { get; set; } = 100_000;

    /// <summary>Gets or sets the max message count threshold.</summary>
    public int MaxMessages { get; set; } = 200;

    /// <summary>Gets or sets whether to preserve system messages during compaction. Default true.</summary>
    public bool PreserveSystemMessages { get; set; } = true;

    /// <summary>Gets or sets how many recent messages to always preserve. Default 5.</summary>
    public int PreserveRecentCount { get; set; } = 5;

    /// <summary>Gets or sets focus instructions for what to preserve in summaries.</summary>
    public string? FocusInstructions { get; set; }

    /// <summary>Gets or sets the compaction strategy to use.</summary>
    public ICompactionStrategy? Strategy { get; set; }
}
