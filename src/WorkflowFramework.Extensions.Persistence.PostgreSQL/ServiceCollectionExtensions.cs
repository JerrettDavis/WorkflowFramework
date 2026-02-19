using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.Persistence.EntityFramework;
using WorkflowFramework.Persistence;

namespace WorkflowFramework.Extensions.Persistence.PostgreSQL;

/// <summary>
/// Extension methods for registering PostgreSQL workflow persistence services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds PostgreSQL-backed workflow state persistence to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPostgreSqlPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<WorkflowDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IWorkflowStateStore, EfCoreWorkflowStateStore>();
        return services;
    }
}
