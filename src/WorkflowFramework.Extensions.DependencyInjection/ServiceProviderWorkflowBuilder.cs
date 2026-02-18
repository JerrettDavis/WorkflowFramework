using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Builder;

namespace WorkflowFramework.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for resolving steps from <see cref="IServiceProvider"/>.
/// </summary>
public static class WorkflowBuilderExtensions
{
    /// <summary>
    /// Adds a step resolved from the service provider.
    /// </summary>
    /// <typeparam name="TStep">The step type.</typeparam>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The workflow builder for chaining.</returns>
    public static IWorkflowBuilder StepFromServices<TStep>(
        this IWorkflowBuilder builder,
        IServiceProvider serviceProvider)
        where TStep : class, IStep
    {
        var step = serviceProvider.GetRequiredService<TStep>();
        return builder.Step(step);
    }

    /// <summary>
    /// Adds a middleware instance resolved from the service provider.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type.</typeparam>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The workflow builder for chaining.</returns>
    public static IWorkflowBuilder UseFromServices<TMiddleware>(
        this IWorkflowBuilder builder,
        IServiceProvider serviceProvider)
        where TMiddleware : class, IWorkflowMiddleware
    {
        var middleware = serviceProvider.GetRequiredService<TMiddleware>();
        return builder.Use(middleware);
    }
}
