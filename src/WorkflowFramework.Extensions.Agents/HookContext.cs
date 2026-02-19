namespace WorkflowFramework.Extensions.Agents;

/// <summary>Context passed to agent hooks.</summary>
public sealed class HookContext
{
    /// <summary>Gets or sets the hook event.</summary>
    public AgentHookEvent Event { get; set; }
    /// <summary>Gets or sets the step name.</summary>
    public string? StepName { get; set; }
    /// <summary>Gets or sets the tool name.</summary>
    public string? ToolName { get; set; }
    /// <summary>Gets or sets the tool arguments JSON.</summary>
    public string? ToolArgs { get; set; }
    /// <summary>Gets or sets the tool result.</summary>
    public ToolResult? ToolResult { get; set; }
    /// <summary>Gets or sets the workflow context.</summary>
    public IWorkflowContext? WorkflowContext { get; set; }
    /// <summary>Gets metadata.</summary>
    public IDictionary<string, object?> Metadata { get; set; } = new Dictionary<string, object?>();
    /// <summary>Gets or sets conversation messages.</summary>
    public IList<ConversationMessage> Messages { get; set; } = new List<ConversationMessage>();
}
