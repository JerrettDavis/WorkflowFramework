namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Role of a message in an agent conversation.
/// </summary>
public enum ConversationRole
{
    /// <summary>System instruction message.</summary>
    System,
    /// <summary>User message.</summary>
    User,
    /// <summary>Assistant/LLM response.</summary>
    Assistant,
    /// <summary>Tool result message.</summary>
    Tool
}
