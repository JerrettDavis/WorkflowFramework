using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace WorkflowFramework.Extensions.Plugins;

/// <summary>
/// Default implementation of <see cref="IWorkflowPluginContext"/>.
/// </summary>
public sealed class WorkflowPluginContext : IWorkflowPluginContext
{
    private readonly ConcurrentDictionary<string, List<Func<IWorkflowContext, Task>>> _hooks = new();

    /// <summary>
    /// Initializes a new instance of <see cref="WorkflowPluginContext"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public WorkflowPluginContext(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <inheritdoc />
    public IServiceCollection Services { get; }

    /// <inheritdoc />
    public void RegisterStep<TStep>() where TStep : class, IStep
    {
        Services.AddTransient<TStep>();
    }

    /// <inheritdoc />
    public void RegisterMiddleware<TMiddleware>() where TMiddleware : class, IWorkflowMiddleware
    {
        Services.AddTransient<IWorkflowMiddleware, TMiddleware>();
    }

    /// <inheritdoc />
    public void RegisterEvents<TEvents>() where TEvents : class, IWorkflowEvents
    {
        Services.AddTransient<IWorkflowEvents, TEvents>();
    }

    /// <inheritdoc />
    public void OnEvent(string eventName, Func<IWorkflowContext, Task> handler)
    {
        var hooks = _hooks.GetOrAdd(eventName, _ => new List<Func<IWorkflowContext, Task>>());
        lock (hooks)
        {
            hooks.Add(handler);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<Func<IWorkflowContext, Task>> GetEventHooks(string eventName)
    {
        if (_hooks.TryGetValue(eventName, out var hooks))
        {
            lock (hooks)
            {
                return hooks.ToArray();
            }
        }
        return Array.Empty<Func<IWorkflowContext, Task>>();
    }
}
