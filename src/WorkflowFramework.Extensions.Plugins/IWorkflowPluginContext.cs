using Microsoft.Extensions.DependencyInjection;

namespace WorkflowFramework.Extensions.Plugins;

/// <summary>
/// Provides access to framework registration points for plugins.
/// </summary>
public interface IWorkflowPluginContext
{
    /// <summary>Gets the service collection for registering services.</summary>
    IServiceCollection Services { get; }

    /// <summary>Registers a step type with the framework.</summary>
    /// <typeparam name="TStep">The step type.</typeparam>
    void RegisterStep<TStep>() where TStep : class, IStep;

    /// <summary>Registers a middleware type with the framework.</summary>
    /// <typeparam name="TMiddleware">The middleware type.</typeparam>
    void RegisterMiddleware<TMiddleware>() where TMiddleware : class, IWorkflowMiddleware;

    /// <summary>Registers an event handler type with the framework.</summary>
    /// <typeparam name="TEvents">The event handler type.</typeparam>
    void RegisterEvents<TEvents>() where TEvents : class, IWorkflowEvents;

    /// <summary>Registers an event hook callback.</summary>
    /// <param name="eventName">The event name.</param>
    /// <param name="handler">The handler delegate.</param>
    void OnEvent(string eventName, Func<IWorkflowContext, Task> handler);

    /// <summary>Gets registered event hooks for a given event name.</summary>
    IReadOnlyList<Func<IWorkflowContext, Task>> GetEventHooks(string eventName);
}
