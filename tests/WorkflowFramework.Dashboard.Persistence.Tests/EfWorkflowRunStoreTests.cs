using Xunit;
using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Persistence;
using WorkflowFramework.Dashboard.Persistence.Entities;

namespace WorkflowFramework.Dashboard.Persistence.Tests;

public sealed class EfWorkflowRunStoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly DashboardDbContext _db;
    private readonly EfWorkflowRunStore _store;
    private const string TestWorkflowId = "wf-1";

    public EfWorkflowRunStoreTests()
    {
        _db = _factory.CreateSeeded();
        // Create the workflow entity that runs will reference
        _db.Workflows.Add(new WorkflowEntity
        {
            Id = TestWorkflowId, OwnerId = "system", Name = "Test Workflow"
        });
        _db.SaveChanges();
        _store = new EfWorkflowRunStore(_db, new WorkflowFramework.Dashboard.Api.Services.AnonymousCurrentUserService());
    }

    [Fact]
    public async Task SaveRunAsync_PersistsNewRun()
    {
        var run = new RunSummary
        {
            RunId = "run-1",
            WorkflowId = TestWorkflowId,
            WorkflowName = "Test",
            Status = "Running",
            StartedAt = DateTimeOffset.UtcNow
        };

        await _store.SaveRunAsync(run);
        var result = await _store.GetRunAsync("run-1");
        result.Should().NotBeNull();
        result!.Status.Should().Be("Running");
    }

    [Fact]
    public async Task SaveRunAsync_UpdatesExistingRun()
    {
        var run = new RunSummary
        {
            RunId = "run-2",
            WorkflowId = TestWorkflowId,
            WorkflowName = "Test",
            Status = "Running",
            StartedAt = DateTimeOffset.UtcNow
        };
        await _store.SaveRunAsync(run);

        run.Status = "Completed";
        run.CompletedAt = DateTimeOffset.UtcNow;
        await _store.SaveRunAsync(run);

        var result = await _store.GetRunAsync("run-2");
        result!.Status.Should().Be("Completed");
        result.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRunsAsync_ReturnsOrderedByStartedAtDesc()
    {
        await _store.SaveRunAsync(new RunSummary
        {
            RunId = "run-a", WorkflowId = TestWorkflowId, WorkflowName = "A",
            Status = "Completed", StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        await _store.SaveRunAsync(new RunSummary
        {
            RunId = "run-b", WorkflowId = TestWorkflowId, WorkflowName = "B",
            Status = "Completed", StartedAt = DateTimeOffset.UtcNow
        });

        var runs = await _store.GetRunsAsync();
        runs[0].RunId.Should().Be("run-b");
        runs[1].RunId.Should().Be("run-a");
    }

    [Fact]
    public async Task GetRunsAsync_RespectsLimit()
    {
        for (int i = 0; i < 5; i++)
            await _store.SaveRunAsync(new RunSummary
            {
                RunId = $"run-{i}", WorkflowId = TestWorkflowId, WorkflowName = "Test",
                Status = "Completed", StartedAt = DateTimeOffset.UtcNow
            });

        var runs = await _store.GetRunsAsync(limit: 3);
        runs.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetRunAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _store.GetRunAsync("nonexistent");
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _db.Dispose();
        _factory.Dispose();
    }
}

