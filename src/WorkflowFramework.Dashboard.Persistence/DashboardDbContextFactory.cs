using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WorkflowFramework.Dashboard.Persistence;

/// <summary>
/// Design-time factory used by EF Core tooling (migrations, scaffolding).
/// </summary>
public sealed class DashboardDbContextFactory : IDesignTimeDbContextFactory<DashboardDbContext>
{
    public DashboardDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DashboardDbContext>()
            .UseSqlite("Data Source=dashboard.db")
            .Options;
        return new DashboardDbContext(options);
    }
}
