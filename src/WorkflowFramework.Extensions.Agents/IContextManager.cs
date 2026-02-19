namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Manages conversation context and supports compaction.
/// </summary>
public interface IContextManager
{
    /// <summary>Adds a message to the conversation.</summary>
    void AddMessage(ConversationMessage message);

    /// <summary>Adds a tool call record as a Tool-role message.</summary>
    void AddToolCall(string toolName, string args, string result);

    /// <summary>Gets all messages in the conversation.</summary>
    IReadOnlyList<ConversationMessage> GetMessages();

    /// <summary>Estimates the total token count of all messages.</summary>
    int EstimateTokenCount();

    /// <summary>Compacts the conversation history.</summary>
    Task<CompactionResult> CompactAsync(CompactionOptions options, CancellationToken ct = default);

    /// <summary>Creates a snapshot of current state.</summary>
    ContextSnapshot CreateSnapshot();

    /// <summary>Restores from a snapshot.</summary>
    void RestoreSnapshot(ContextSnapshot snapshot);

    /// <summary>Clears all messages.</summary>
    void Clear();
}
