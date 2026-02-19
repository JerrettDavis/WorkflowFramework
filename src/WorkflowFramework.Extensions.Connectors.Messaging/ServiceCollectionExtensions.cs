using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WorkflowFramework.Extensions.Connectors.Abstractions;

namespace WorkflowFramework.Extensions.Connectors.Messaging;

/// <summary>
/// DI extension methods for messaging connectors.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers an <see cref="InMemoryMessageConnector"/> as a singleton.
    /// </summary>
    public static IServiceCollection AddInMemoryMessageConnector(
        this IServiceCollection services,
        string name = "in-memory")
    {
        var connector = new InMemoryMessageConnector(name);
        services.TryAddSingleton<IMessageConnector>(connector);
        services.TryAddSingleton<InMemoryMessageConnector>(connector);
        return services;
    }

    /// <summary>
    /// Registers a message connector instance.
    /// </summary>
    public static IServiceCollection AddMessageConnector<T>(
        this IServiceCollection services,
        T connector) where T : class, IMessageConnector
    {
        services.AddSingleton<IMessageConnector>(connector);
        services.AddSingleton(connector);
        return services;
    }
}
