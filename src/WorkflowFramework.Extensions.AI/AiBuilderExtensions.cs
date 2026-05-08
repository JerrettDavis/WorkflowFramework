using WorkflowFramework.Builder;

namespace WorkflowFramework.Extensions.AI;

/// <summary>
/// Fluent builder extensions for AI workflow steps.
/// </summary>
public static class AiBuilderExtensions
{
    /// <summary>
    /// Adds an LLM call step to the workflow.
    /// </summary>
    public static IWorkflowBuilder LlmCall(
        this IWorkflowBuilder builder,
        IAgentProvider provider,
        Action<LlmCallOptions>? configure = null)
    {
        var options = new LlmCallOptions();
        configure?.Invoke(options);
        return builder.Step(new LlmCallStep(provider, options));
    }

    /// <summary>
    /// Adds an agent decision step to the workflow.
    /// </summary>
    public static IWorkflowBuilder AgentDecision(
        this IWorkflowBuilder builder,
        IAgentProvider provider,
        Action<AgentDecisionOptions>? configure = null)
    {
        var options = new AgentDecisionOptions();
        configure?.Invoke(options);
        return builder.Step(new AgentDecisionStep(provider, options));
    }

    /// <summary>
    /// Adds an agent planning step to the workflow.
    /// </summary>
    public static IWorkflowBuilder AgentPlan(
        this IWorkflowBuilder builder,
        IAgentProvider provider,
        Action<AgentPlanOptions>? configure = null)
    {
        var options = new AgentPlanOptions();
        configure?.Invoke(options);
        return builder.Step(new AgentPlanStep(provider, options));
    }
}
