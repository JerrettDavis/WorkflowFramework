using Microsoft.EntityFrameworkCore;
using WorkflowFramework.Extensions.Persistence.EntityFramework;

namespace WorkflowFramework.Extensions.Persistence.PostgreSQL;

/// <summary>
/// PostgreSQL-specific <see cref="WorkflowDbContext"/> configured with Npgsql.
/// </summary>
public class PostgreSqlWorkflowDbContext : WorkflowDbContext
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of <see cref="PostgreSqlWorkflowDbContext"/>.
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string.</param>
    public PostgreSqlWorkflowDbContext(string connectionString)
        : base(CreateOptions(connectionString))
    {
        _connectionString = connectionString;
    }

    private static DbContextOptions<WorkflowDbContext> CreateOptions(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<WorkflowDbContext>();
        builder.UseNpgsql(connectionString);
        return builder.Options;
    }
}
