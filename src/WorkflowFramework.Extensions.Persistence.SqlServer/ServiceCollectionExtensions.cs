using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.Persistence.EntityFramework;
using WorkflowFramework.Persistence;

namespace WorkflowFramework.Extensions.Persistence.SqlServer;

/// <summary>
/// Extension methods for registering SQL Server workflow persistence services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds SQL Server-backed workflow state persistence to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSqlServerPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<WorkflowDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddScoped<IWorkflowStateStore, EfCoreWorkflowStateStore>();
        return services;
    }
}
