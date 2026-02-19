using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace WorkflowFramework.Extensions.Distributed.PostgreSQL;

/// <summary>
/// Extension methods for configuring PostgreSQL-based distributed workflow services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds PostgreSQL-based distributed lock and workflow queue implementations.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPostgreSqlDistributed(this IServiceCollection services, string connectionString)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (connectionString is null) throw new ArgumentNullException(nameof(connectionString));

        Func<NpgsqlConnection> connectionFactory = () => new NpgsqlConnection(connectionString);

        services.AddSingleton<IDistributedLock>(new PostgreSqlDistributedLock(connectionFactory));
        services.AddSingleton<IWorkflowQueue>(new PostgreSqlWorkflowQueue(connectionFactory));

        return services;
    }
}
