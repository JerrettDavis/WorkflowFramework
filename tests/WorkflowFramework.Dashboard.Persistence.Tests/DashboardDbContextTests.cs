using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using WorkflowFramework.Dashboard.Persistence.Entities;

namespace WorkflowFramework.Dashboard.Persistence.Tests;

public sealed class DashboardDbContextTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    [Fact]
    public void EnsureCreated_CreatesAllTables()
    {
        using var db = _factory.Create();
        // Verify all DbSets are queryable (tables exist)
        db.Users.ToList().Should().BeEmpty();
        db.Workflows.ToList().Should().BeEmpty();
        db.WorkflowVersions.ToList().Should().BeEmpty();
        db.WorkflowRuns.ToList().Should().BeEmpty();
        db.StepRuns.ToList().Should().BeEmpty();
        db.AuditEntries.ToList().Should().BeEmpty();
        db.UserSettings.ToList().Should().BeEmpty();
    }

    [Fact]
    public void SoftDelete_QueryFilter_ExcludesDeletedWorkflows()
    {
        using var db = _factory.CreateSeeded();
        db.Workflows.Add(new WorkflowEntity
        {
            Id = "wf-active", OwnerId = "system", Name = "Active"
        });
        db.Workflows.Add(new WorkflowEntity
        {
            Id = "wf-deleted", OwnerId = "system", Name = "Deleted", IsDeleted = true
        });
        db.SaveChanges();

        // Default query should exclude deleted
        db.Workflows.ToList().Should().HaveCount(1);
        db.Workflows.First().Id.Should().Be("wf-active");

        // IgnoreQueryFilters should include deleted
        db.Workflows.IgnoreQueryFilters().ToList().Should().HaveCount(2);
    }

    [Fact]
    public void UniqueIndex_Username_EnforcedOnDashboardUser()
    {
        using var db = _factory.Create();
        db.Users.Add(new DashboardUser { Id = "u1", Username = "alice" });
        db.SaveChanges();

        db.Users.Add(new DashboardUser { Id = "u2", Username = "alice" });
        var act = () => db.SaveChanges();
        act.Should().Throw<DbUpdateException>();
    }

    [Fact]
    public void UniqueIndex_WorkflowVersion_EnforcedOnWorkflowIdVersionNumber()
    {
        using var db = _factory.CreateSeeded();
        db.Workflows.Add(new WorkflowEntity { Id = "wf-1", OwnerId = "system", Name = "Test" });
        db.SaveChanges();

        db.WorkflowVersions.Add(new WorkflowVersionEntity
        {
            Id = "v1", WorkflowId = "wf-1", VersionNumber = 1, DefinitionJson = "{}"
        });
        db.SaveChanges();

        db.WorkflowVersions.Add(new WorkflowVersionEntity
        {
            Id = "v2", WorkflowId = "wf-1", VersionNumber = 1, DefinitionJson = "{}"
        });
        var act = () => db.SaveChanges();
        act.Should().Throw<DbUpdateException>();
    }

    [Fact]
    public void UniqueIndex_UserSettings_EnforcedOnUserIdKey()
    {
        using var db = _factory.CreateSeeded();
        db.UserSettings.Add(new UserSettingEntity { Id = "s1", UserId = "system", Key = "theme", Value = "dark" });
        db.SaveChanges();

        db.UserSettings.Add(new UserSettingEntity { Id = "s2", UserId = "system", Key = "theme", Value = "light" });
        var act = () => db.SaveChanges();
        act.Should().Throw<DbUpdateException>();
    }

    public void Dispose() => _factory.Dispose();
}

