using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// DI extension methods for agent tooling.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers agent tooling services including <see cref="ToolRegistry"/>.
    /// Auto-discovers <see cref="IToolProvider"/> implementations from DI.
    /// </summary>
    public static IServiceCollection AddAgentTooling(
        this IServiceCollection services,
        Action<AgentToolingOptions>? configure = null)
    {
        var options = new AgentToolingOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<ToolRegistry>(sp =>
        {
            var registry = new ToolRegistry();
            foreach (var provider in sp.GetServices<IToolProvider>())
            {
                registry.Register(provider);
            }
            return registry;
        });

        return services;
    }
}
