using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Builder;

namespace WorkflowFramework.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering WorkflowFramework services with <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds WorkflowFramework core services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWorkflowFramework(this IServiceCollection services)
    {
        services.AddTransient<IWorkflowBuilder, WorkflowBuilder>();
        services.AddTransient(typeof(IWorkflowBuilder<>), typeof(WorkflowBuilder<>));
        return services;
    }

    /// <summary>
    /// Registers a step type with the service collection.
    /// </summary>
    /// <typeparam name="TStep">The step type to register.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStep<TStep>(this IServiceCollection services)
        where TStep : class, IStep
    {
        services.AddTransient<TStep>();
        return services;
    }

    /// <summary>
    /// Registers a typed step with the service collection.
    /// </summary>
    /// <typeparam name="TStep">The step type to register.</typeparam>
    /// <typeparam name="TData">The workflow data type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStep<TStep, TData>(this IServiceCollection services)
        where TStep : class, IStep<TData>
        where TData : class
    {
        services.AddTransient<TStep>();
        return services;
    }

    /// <summary>
    /// Registers a middleware type with the service collection.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type to register.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWorkflowMiddleware<TMiddleware>(this IServiceCollection services)
        where TMiddleware : class, IWorkflowMiddleware
    {
        services.AddTransient<IWorkflowMiddleware, TMiddleware>();
        return services;
    }

    /// <summary>
    /// Registers workflow event handlers with the service collection.
    /// </summary>
    /// <typeparam name="TEvents">The event handler type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWorkflowEvents<TEvents>(this IServiceCollection services)
        where TEvents : class, IWorkflowEvents
    {
        services.AddTransient<IWorkflowEvents, TEvents>();
        return services;
    }
}
