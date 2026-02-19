namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Strategy for compacting conversation history.
/// </summary>
public interface ICompactionStrategy
{
    /// <summary>Gets the strategy name.</summary>
    string Name { get; }

    /// <summary>
    /// Summarizes messages into a compact form.
    /// </summary>
    Task<string> SummarizeAsync(IReadOnlyList<ConversationMessage> messages, CompactionOptions options, CancellationToken ct = default);
}
