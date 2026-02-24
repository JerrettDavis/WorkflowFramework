using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Dashboard.Api.Services;
using WorkflowFramework.Dashboard.Persistence;
using WorkflowFramework.Dashboard.Persistence.Entities;

namespace WorkflowFramework.Dashboard.Api.Persistence;

/// <summary>
/// Extension methods to register EF Core persistence for the dashboard,
/// replacing in-memory stores with SQLite-backed implementations.
/// </summary>
public static class DashboardPersistenceExtensions
{
    /// <summary>
    /// Adds EF Core + SQLite persistence for the dashboard, replacing in-memory stores.
    /// </summary>
    public static IServiceCollection AddDashboardPersistence(
        this IServiceCollection services,
        string? connectionString = null)
    {
        services.AddDbContext<DashboardDbContext>(options =>
        {
            options.UseSqlite(connectionString ?? "Data Source=dashboard.db");
        });

        // Replace in-memory workflow store with EF Core
        RemoveService<IWorkflowDefinitionStore>(services);
        services.AddScoped<IWorkflowDefinitionStore, EfWorkflowDefinitionStore>();

        // Replace in-memory audit, versioning, settings with EF-backed stores
        RemoveService<IAuditTrailService>(services);
        services.AddScoped<IAuditTrailService, EfAuditTrailStore>();

        RemoveService<IWorkflowVersioningService>(services);
        services.AddScoped<IWorkflowVersioningService, EfWorkflowVersioningStore>();

        RemoveService<IDashboardSettingsService>(services);
        services.AddScoped<IDashboardSettingsService, EfSettingsStore>();

        services.AddScoped<EfWorkflowRunStore>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }

    /// <summary>
    /// Ensures the dashboard database is created and seeded with sample workflows.
    /// </summary>
    public static async Task InitializeDashboardDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DashboardDbContext>();
        await db.Database.EnsureCreatedAsync();

        // Ensure system user exists for pre-auth single-user mode
        if (!await db.Users.AnyAsync(u => u.Id == EfWorkflowDefinitionStore.SystemUserId))
        {
            db.Users.Add(new DashboardUser
            {
                Id = EfWorkflowDefinitionStore.SystemUserId,
                Username = "system",
                DisplayName = "System",
                CreatedAt = DateTimeOffset.UtcNow,
                LastLoginAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Seed sample workflows
        var store = scope.ServiceProvider.GetRequiredService<IWorkflowDefinitionStore>();
        await SampleWorkflowSeeder.SeedAsync(store);
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(T));
        if (descriptor is not null) services.Remove(descriptor);
    }
}
