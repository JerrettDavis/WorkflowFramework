using Microsoft.EntityFrameworkCore;

namespace WorkflowFramework.Extensions.Persistence.EntityFramework;

/// <summary>
/// DbContext for workflow state persistence.
/// </summary>
public class WorkflowDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of <see cref="WorkflowDbContext"/>.
    /// </summary>
    /// <param name="options">The DbContext options.</param>
    public WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : base(options) { }

    /// <summary>Gets or sets the workflow states DbSet.</summary>
    public DbSet<WorkflowStateEntity> WorkflowStates { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkflowStateEntity>(e =>
        {
            e.HasKey(x => x.WorkflowId);
            e.Property(x => x.WorkflowId).HasMaxLength(64);
            e.Property(x => x.CorrelationId).HasMaxLength(64);
            e.Property(x => x.WorkflowName).HasMaxLength(256);
            e.HasIndex(x => x.CorrelationId);
        });
    }
}
