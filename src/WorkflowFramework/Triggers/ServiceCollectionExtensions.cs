#if NET8_0_OR_GREATER
using Microsoft.Extensions.DependencyInjection;

namespace WorkflowFramework.Triggers;

/// <summary>
/// DI registration extensions for the trigger system.
/// </summary>
public static class TriggerServiceCollectionExtensions
{
    /// <summary>
    /// Adds workflow trigger services including the factory and hosted trigger service.
    /// </summary>
    public static IServiceCollection AddWorkflowTriggers(this IServiceCollection services)
    {
        services.AddSingleton<TriggerSourceFactory>();
        services.AddSingleton<ITriggerSourceFactory>(sp => sp.GetRequiredService<TriggerSourceFactory>());
        services.AddSingleton<WorkflowTriggerService>();
        services.AddHostedService(sp => sp.GetRequiredService<WorkflowTriggerService>());
        return services;
    }

    /// <summary>
    /// Adds workflow trigger services with custom factory configuration.
    /// </summary>
    public static IServiceCollection AddWorkflowTriggers(
        this IServiceCollection services,
        Action<TriggerSourceFactory> configure)
    {
        services.AddSingleton<TriggerSourceFactory>(sp =>
        {
            var factory = new TriggerSourceFactory();
            configure(factory);
            return factory;
        });
        services.AddSingleton<ITriggerSourceFactory>(sp => sp.GetRequiredService<TriggerSourceFactory>());
        services.AddSingleton<WorkflowTriggerService>();
        services.AddHostedService(sp => sp.GetRequiredService<WorkflowTriggerService>());
        return services;
    }
}
#endif
