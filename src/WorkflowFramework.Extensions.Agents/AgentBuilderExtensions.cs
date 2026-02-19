using WorkflowFramework.Builder;
using WorkflowFramework.Extensions.AI;

namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Extension methods for <see cref="IWorkflowBuilder"/> to add agent steps.
/// </summary>
public static class AgentBuilderExtensions
{
    /// <summary>
    /// Adds an autonomous agent loop step to the workflow.
    /// </summary>
    public static IWorkflowBuilder AgentLoop(
        this IWorkflowBuilder builder,
        IAgentProvider provider,
        ToolRegistry registry,
        Action<AgentLoopOptions>? configure = null)
    {
        var options = new AgentLoopOptions();
        configure?.Invoke(options);
        return builder.Step(new AgentLoopStep(provider, registry, options));
    }

    /// <summary>
    /// Adds a tool call step to the workflow.
    /// </summary>
    public static IWorkflowBuilder CallTool(
        this IWorkflowBuilder builder,
        ToolRegistry registry,
        string toolName,
        string argumentsTemplate,
        string? stepName = null)
    {
        return builder.Step(new ToolCallStep(registry, toolName, argumentsTemplate, stepName));
    }
}
