using System.Diagnostics;

namespace WorkflowFramework.Extensions.Agents.Diagnostics;

/// <summary>
/// Provides the <see cref="ActivitySource"/> for agent loop tracing.
/// </summary>
public static class AgentActivitySource
{
    /// <summary>The name of the activity source.</summary>
    public const string Name = "WorkflowFramework.Agents";

    /// <summary>Gets the shared <see cref="ActivitySource"/> instance.</summary>
    public static ActivitySource Instance { get; } = new(Name);

    // Span names

    /// <summary>Span name for the overall agent loop.</summary>
    public const string AgentLoop = "agent.loop";
    /// <summary>Span name for a single agent iteration.</summary>
    public const string AgentIteration = "agent.iteration";
    /// <summary>Span name for a tool call.</summary>
    public const string ToolCall = "agent.tool_call";
    /// <summary>Span name for an MCP server invocation.</summary>
    public const string McpInvoke = "agent.mcp.invoke";
    /// <summary>Span name for context compaction.</summary>
    public const string ContextCompaction = "agent.context.compaction";

    // Tag names

    /// <summary>Tag for the step name.</summary>
    public const string TagStepName = "agent.step.name";
    /// <summary>Tag for the current iteration number.</summary>
    public const string TagIteration = "agent.iteration.number";
    /// <summary>Tag for the total iteration count.</summary>
    public const string TagIterationTotal = "agent.iteration.total";
    /// <summary>Tag for the tool name.</summary>
    public const string TagToolName = "agent.tool.name";
    /// <summary>Tag indicating whether the tool call resulted in an error.</summary>
    public const string TagToolIsError = "agent.tool.is_error";
    /// <summary>Tag for the provider type name.</summary>
    public const string TagProviderType = "agent.provider.type";
    /// <summary>Tag for the provider name.</summary>
    public const string TagProviderName = "agent.provider.name";
    /// <summary>Tag for prompt token count.</summary>
    public const string TagPromptTokens = "agent.tokens.prompt";
    /// <summary>Tag for completion token count.</summary>
    public const string TagCompletionTokens = "agent.tokens.completion";
    /// <summary>Tag for total token count.</summary>
    public const string TagTotalTokens = "agent.tokens.total";
    /// <summary>Tag for original message count before compaction.</summary>
    public const string TagCompactionOriginalMessages = "agent.compaction.original_messages";
    /// <summary>Tag for compacted message count.</summary>
    public const string TagCompactionCompactedMessages = "agent.compaction.compacted_messages";
    /// <summary>Tag for original token estimate before compaction.</summary>
    public const string TagCompactionOriginalTokens = "agent.compaction.original_tokens";
    /// <summary>Tag for compacted token estimate.</summary>
    public const string TagCompactionCompactedTokens = "agent.compaction.compacted_tokens";
    /// <summary>Tag for the number of tool calls in an iteration.</summary>
    public const string TagToolCallCount = "agent.tool_call.count";
    /// <summary>Tag for the MCP server name.</summary>
    public const string TagMcpServerName = "agent.mcp.server_name";
}
