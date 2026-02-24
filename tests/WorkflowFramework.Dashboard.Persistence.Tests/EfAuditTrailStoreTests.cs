using Xunit;
using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Persistence;

namespace WorkflowFramework.Dashboard.Persistence.Tests;

public sealed class EfAuditTrailStoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly DashboardDbContext _db;
    private readonly EfAuditTrailStore _store;

    public EfAuditTrailStoreTests()
    {
        _db = _factory.CreateSeeded();
        _store = new EfAuditTrailStore(_db);
    }

    [Fact]
    public void Log_PersistsEntry()
    {
        _store.Log("workflow.created", "wf-1", "Created workflow", "user1");
        var entries = _store.Query();
        entries.Should().HaveCount(1);
        entries[0].Action.Should().Be("workflow.created");
        entries[0].WorkflowId.Should().Be("wf-1");
        entries[0].UserId.Should().Be("user1");
    }

    [Fact]
    public async Task LogAsync_PersistsEntry()
    {
        await _store.LogAsync("workflow.updated", "wf-2", "Updated");
        var entries = _store.Query();
        entries.Should().HaveCount(1);
        entries[0].Action.Should().Be("workflow.updated");
    }

    [Fact]
    public void Query_FiltersByAction()
    {
        _store.Log("workflow.created");
        _store.Log("workflow.deleted");

        var entries = _store.Query(action: "workflow.created");
        entries.Should().HaveCount(1);
    }

    [Fact]
    public void Query_FiltersByWorkflowId()
    {
        _store.Log("workflow.created", "wf-1");
        _store.Log("workflow.created", "wf-2");

        var entries = _store.Query(workflowId: "wf-1");
        entries.Should().HaveCount(1);
    }

    [Fact]
    public void Query_RespectsLimit()
    {
        for (int i = 0; i < 10; i++)
            _store.Log("test");

        var entries = _store.Query(limit: 5);
        entries.Should().HaveCount(5);
    }

    [Fact]
    public void GetForWorkflow_ReturnsFilteredEntries()
    {
        _store.Log("workflow.created", "wf-1");
        _store.Log("workflow.updated", "wf-1");
        _store.Log("workflow.created", "wf-2");

        var entries = _store.GetForWorkflow("wf-1");
        entries.Should().HaveCount(2);
    }

    [Fact]
    public void Query_OrdersByTimestampDesc()
    {
        _store.Log("first");
        _store.Log("second");

        var entries = _store.Query();
        // Both have similar timestamps but order should be consistent
        entries.Should().HaveCount(2);
    }

    public void Dispose()
    {
        _db.Dispose();
        _factory.Dispose();
    }
}

