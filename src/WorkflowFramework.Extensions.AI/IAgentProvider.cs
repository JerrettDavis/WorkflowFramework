namespace WorkflowFramework.Extensions.AI;

/// <summary>
/// Abstraction for AI agent providers (e.g., Semantic Kernel, LangChain).
/// </summary>
public interface IAgentProvider
{
    /// <summary>Gets the provider name.</summary>
    string Name { get; }

    /// <summary>
    /// Sends a prompt to the LLM and returns the response.
    /// </summary>
    /// <param name="request">The LLM request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The LLM response.</returns>
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes a routing decision based on the provided context.
    /// </summary>
    /// <param name="request">The decision request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The chosen route.</returns>
    Task<string> DecideAsync(AgentDecisionRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a request to an LLM.
/// </summary>
public sealed class LlmRequest
{
    /// <summary>Gets or sets the prompt template.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Gets or sets context variables to inject into the prompt.</summary>
    public IDictionary<string, object?> Variables { get; set; } = new Dictionary<string, object?>();

    /// <summary>Gets or sets the model to use.</summary>
    public string? Model { get; set; }

    /// <summary>Gets or sets the temperature.</summary>
    public double? Temperature { get; set; }

    /// <summary>Gets or sets the max tokens.</summary>
    public int? MaxTokens { get; set; }

    /// <summary>Gets or sets available tool/function definitions.</summary>
    public IList<AgentTool> Tools { get; set; } = new List<AgentTool>();
}

/// <summary>
/// Represents an LLM response.
/// </summary>
public sealed class LlmResponse
{
    /// <summary>Gets or sets the text response.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets any tool calls the model requested.</summary>
    public IList<ToolCall> ToolCalls { get; set; } = new List<ToolCall>();

    /// <summary>Gets or sets the finish reason.</summary>
    public string? FinishReason { get; set; }

    /// <summary>Gets or sets the token usage.</summary>
    public TokenUsage? Usage { get; set; }
}

/// <summary>
/// Represents a tool/function available to the agent.
/// </summary>
public sealed class AgentTool
{
    /// <summary>Gets or sets the tool name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the tool description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the tool's parameter schema (JSON).</summary>
    public string? ParametersSchema { get; set; }
}

/// <summary>
/// Represents a tool call from the LLM.
/// </summary>
public sealed class ToolCall
{
    /// <summary>Gets or sets the tool name.</summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>Gets or sets the arguments (JSON).</summary>
    public string Arguments { get; set; } = string.Empty;
}

/// <summary>
/// Represents token usage information.
/// </summary>
public sealed class TokenUsage
{
    /// <summary>Gets or sets prompt tokens used.</summary>
    public int PromptTokens { get; set; }

    /// <summary>Gets or sets completion tokens used.</summary>
    public int CompletionTokens { get; set; }

    /// <summary>Gets or sets total tokens used.</summary>
    public int TotalTokens { get; set; }
}

/// <summary>
/// Represents a decision request for an AI agent.
/// </summary>
public sealed class AgentDecisionRequest
{
    /// <summary>Gets or sets the decision prompt.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Gets or sets available routes/options to choose from.</summary>
    public IList<string> Options { get; set; } = new List<string>();

    /// <summary>Gets or sets context variables.</summary>
    public IDictionary<string, object?> Variables { get; set; } = new Dictionary<string, object?>();
}
