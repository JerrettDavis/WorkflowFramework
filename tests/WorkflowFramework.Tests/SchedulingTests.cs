using FluentAssertions;
using WorkflowFramework.Extensions.Scheduling;
using WorkflowFramework.Registry;
using Xunit;

namespace WorkflowFramework.Tests;

public class SchedulingTests
{
    [Fact]
    public void CronParser_ParsesSimpleExpression()
    {
        // Every day at midnight
        var next = CronParser.GetNextOccurrence("0 0 * * *", new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero));
        next.Should().NotBeNull();
        next!.Value.Hour.Should().Be(0);
        next.Value.Minute.Should().Be(0);
        next.Value.Day.Should().Be(2);
    }

    [Fact]
    public void CronParser_ParsesRange()
    {
        // Every minute between hours 9-17
        var next = CronParser.GetNextOccurrence("0 9-17 * * *", new DateTimeOffset(2025, 1, 1, 8, 0, 0, TimeSpan.Zero));
        next.Should().NotBeNull();
        next!.Value.Hour.Should().Be(9);
    }

    [Fact]
    public void CronParser_InvalidFormat_Throws()
    {
        var act = () => CronParser.GetNextOccurrence("bad", DateTimeOffset.UtcNow);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public async Task InMemoryScheduler_ScheduleAndGetPending()
    {
        var registry = new WorkflowRegistry();
        registry.Register("test", () => Workflow.Create("test").Build());

        using var scheduler = new InMemoryWorkflowScheduler(registry);
        var context = new WorkflowContext();
        var id = await scheduler.ScheduleAsync("test", DateTimeOffset.UtcNow.AddHours(1), context);

        var pending = await scheduler.GetPendingAsync();
        pending.Should().ContainSingle(p => p.Id == id);
    }

    [Fact]
    public async Task InMemoryScheduler_CancelRemovesSchedule()
    {
        var registry = new WorkflowRegistry();
        using var scheduler = new InMemoryWorkflowScheduler(registry);

        var id = await scheduler.ScheduleAsync("test", DateTimeOffset.UtcNow.AddHours(1), new WorkflowContext());
        var cancelled = await scheduler.CancelAsync(id);

        cancelled.Should().BeTrue();
        (await scheduler.GetPendingAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task InMemoryApproval_ApproveCompletes()
    {
        var service = new InMemoryApprovalService();
        var config = new ApprovalConfig { Name = "Test Approval" };

        var approvalTask = service.RequestApprovalAsync("wf-1", config);

        service.Approve("wf-1");

        var result = await approvalTask;
        result.Should().BeTrue();
    }

    [Fact]
    public async Task InMemoryApproval_RejectCompletes()
    {
        var service = new InMemoryApprovalService();
        var config = new ApprovalConfig { Name = "Test Approval" };

        var approvalTask = service.RequestApprovalAsync("wf-1", config);

        service.Reject("wf-1");

        var result = await approvalTask;
        result.Should().BeFalse();
    }
}
