using Microsoft.EntityFrameworkCore;
using WorkflowFramework.Dashboard.Persistence.Entities;

namespace WorkflowFramework.Dashboard.Persistence;

/// <summary>
/// EF Core DbContext for the dashboard persistence layer.
/// Supports SQLite by default; architecture allows swapping to PostgreSQL/SQL Server.
/// </summary>
public sealed class DashboardDbContext : DbContext
{
    public DashboardDbContext(DbContextOptions<DashboardDbContext> options) : base(options) { }

    public DbSet<DashboardUser> Users => Set<DashboardUser>();
    public DbSet<WorkflowEntity> Workflows => Set<WorkflowEntity>();
    public DbSet<WorkflowVersionEntity> WorkflowVersions => Set<WorkflowVersionEntity>();
    public DbSet<WorkflowRunEntity> WorkflowRuns => Set<WorkflowRunEntity>();
    public DbSet<StepRunEntity> StepRuns => Set<StepRunEntity>();
    public DbSet<AuditEntryEntity> AuditEntries => Set<AuditEntryEntity>();
    public DbSet<UserSettingEntity> UserSettings => Set<UserSettingEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // DashboardUser
        modelBuilder.Entity<DashboardUser>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Username).IsUnique();
        });

        // WorkflowEntity
        modelBuilder.Entity<WorkflowEntity>(e =>
        {
            e.HasKey(w => w.Id);
            e.HasIndex(w => w.OwnerId);
            e.HasIndex(w => w.Name);
            e.HasIndex(w => w.IsDeleted);
            e.HasQueryFilter(w => !w.IsDeleted);
            e.HasOne(w => w.Owner).WithMany(u => u.Workflows).HasForeignKey(w => w.OwnerId);
        });

        // WorkflowVersionEntity
        modelBuilder.Entity<WorkflowVersionEntity>(e =>
        {
            e.HasKey(v => v.Id);
            e.HasIndex(v => new { v.WorkflowId, v.VersionNumber }).IsUnique();
            e.HasOne(v => v.Workflow).WithMany(w => w.Versions).HasForeignKey(v => v.WorkflowId);
        });

        // WorkflowRunEntity
        modelBuilder.Entity<WorkflowRunEntity>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.WorkflowId);
            e.HasIndex(r => r.UserId);
            e.HasIndex(r => r.Status);
            e.HasIndex(r => r.StartedAt);
            e.HasOne(r => r.Workflow).WithMany(w => w.Runs).HasForeignKey(r => r.WorkflowId);
            e.HasOne(r => r.User).WithMany(u => u.Runs).HasForeignKey(r => r.UserId);
        });

        // StepRunEntity
        modelBuilder.Entity<StepRunEntity>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.RunId);
            e.HasOne(s => s.Run).WithMany(r => r.StepRuns).HasForeignKey(s => s.RunId);
        });

        // AuditEntryEntity
        modelBuilder.Entity<AuditEntryEntity>(e =>
        {
            e.HasKey(a => a.Id);
            e.HasIndex(a => a.WorkflowId);
            e.HasIndex(a => a.Action);
            e.HasIndex(a => a.Timestamp);
        });

        // UserSettingEntity
        modelBuilder.Entity<UserSettingEntity>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => new { s.UserId, s.Key }).IsUnique();
            e.HasOne(s => s.User).WithMany(u => u.Settings).HasForeignKey(s => s.UserId);
        });
    }
}
