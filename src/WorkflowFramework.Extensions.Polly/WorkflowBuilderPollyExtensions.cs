using Polly;
using WorkflowFramework.Builder;

namespace WorkflowFramework.Extensions.Polly;

/// <summary>
/// Extension methods for adding Polly resilience to workflow builders.
/// </summary>
public static class WorkflowBuilderPollyExtensions
{
    /// <summary>
    /// Adds Polly resilience middleware with the given pipeline.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="pipeline">The Polly resilience pipeline.</param>
    /// <returns>The workflow builder for chaining.</returns>
    public static IWorkflowBuilder UseResilience(this IWorkflowBuilder builder, ResiliencePipeline pipeline)
    {
        return builder.Use(new ResilienceMiddleware(pipeline));
    }

    /// <summary>
    /// Adds Polly resilience middleware configured via a builder action.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="configure">An action to configure the resilience pipeline.</param>
    /// <returns>The workflow builder for chaining.</returns>
    public static IWorkflowBuilder UseResilience(
        this IWorkflowBuilder builder,
        Action<ResiliencePipelineBuilder> configure)
    {
        var pipelineBuilder = new ResiliencePipelineBuilder();
        configure(pipelineBuilder);
        return builder.Use(new ResilienceMiddleware(pipelineBuilder.Build()));
    }
}
