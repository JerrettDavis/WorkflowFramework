using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Services;
using Xunit;

namespace WorkflowFramework.Dashboard.Api.Tests;

public class AuditTrailServiceTests
{
    private readonly AuditTrailService _sut = new();

    [Fact]
    public void Log_CreatesEntry()
    {
        _sut.Log("workflow.created", "wf1", "Created workflow");
        var entries = _sut.Query();
        entries.Should().HaveCount(1);
        entries[0].Action.Should().Be("workflow.created");
        entries[0].WorkflowId.Should().Be("wf1");
    }

    [Fact]
    public void Query_FiltersByAction()
    {
        _sut.Log("workflow.created", "wf1");
        _sut.Log("workflow.updated", "wf1");
        _sut.Log("run.started", "wf1");

        _sut.Query(action: "workflow.created").Should().HaveCount(1);
    }

    [Fact]
    public void Query_FiltersByWorkflowId()
    {
        _sut.Log("workflow.created", "wf1");
        _sut.Log("workflow.created", "wf2");

        _sut.Query(workflowId: "wf1").Should().HaveCount(1);
    }

    [Fact]
    public void Query_FiltersByUserId()
    {
        _sut.Log("workflow.created", userId: "user1");
        _sut.Log("workflow.created", userId: "user2");

        _sut.Query(userId: "user1").Should().HaveCount(1);
    }

    [Fact]
    public void Query_FiltersByDateRange()
    {
        _sut.Log("old.action");
        var entries = _sut.Query(from: DateTimeOffset.UtcNow.AddSeconds(-1), to: DateTimeOffset.UtcNow.AddSeconds(1));
        entries.Should().HaveCount(1);

        entries = _sut.Query(from: DateTimeOffset.UtcNow.AddHours(1));
        entries.Should().BeEmpty();
    }

    [Fact]
    public void Query_RespectsLimit()
    {
        for (int i = 0; i < 10; i++)
            _sut.Log("action");
        _sut.Query(limit: 5).Should().HaveCount(5);
    }

    [Fact]
    public void GetForWorkflow_ReturnsOnlyThatWorkflow()
    {
        _sut.Log("action", "wf1");
        _sut.Log("action", "wf2");
        _sut.Log("action", "wf1");

        _sut.GetForWorkflow("wf1").Should().HaveCount(2);
    }

    [Fact]
    public void Query_OrdersByTimestampDescending()
    {
        _sut.Log("first", details: "1");
        _sut.Log("second", details: "2");
        var entries = _sut.Query();
        entries[0].Details.Should().Be("2");
    }
}
