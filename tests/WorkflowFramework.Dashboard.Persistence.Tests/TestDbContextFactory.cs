using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WorkflowFramework.Dashboard.Persistence;
using WorkflowFramework.Dashboard.Persistence.Entities;

namespace WorkflowFramework.Dashboard.Persistence.Tests;

/// <summary>
/// Creates in-memory SQLite databases for testing.
/// </summary>
internal sealed class TestDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    public DashboardDbContext Create()
    {
        var options = new DbContextOptionsBuilder<DashboardDbContext>()
            .UseSqlite(_connection)
            .Options;

        var db = new DashboardDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    /// <summary>Creates db and seeds the system user.</summary>
    public DashboardDbContext CreateSeeded()
    {
        var db = Create();
        if (!db.Users.Any(u => u.Id == "system"))
        {
            db.Users.Add(new DashboardUser
            {
                Id = "system",
                Username = "system",
                DisplayName = "System"
            });
            db.SaveChanges();
        }
        return db;
    }

    public void Dispose() => _connection.Dispose();
}
