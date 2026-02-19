using Microsoft.EntityFrameworkCore;
using WorkflowFramework.Extensions.Persistence.EntityFramework;

namespace WorkflowFramework.Extensions.Persistence.SqlServer;

/// <summary>
/// SQL Server-specific <see cref="WorkflowDbContext"/> configured with the SQL Server provider.
/// </summary>
public class SqlServerWorkflowDbContext : WorkflowDbContext
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of <see cref="SqlServerWorkflowDbContext"/>.
    /// </summary>
    /// <param name="connectionString">The SQL Server connection string.</param>
    public SqlServerWorkflowDbContext(string connectionString)
        : base(CreateOptions(connectionString))
    {
        _connectionString = connectionString;
    }

    private static DbContextOptions<WorkflowDbContext> CreateOptions(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<WorkflowDbContext>();
        builder.UseSqlServer(connectionString);
        return builder.Options;
    }
}
