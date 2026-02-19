using FluentAssertions;
using WorkflowFramework.Extensions.Scheduling;
using WorkflowFramework.Registry;
using Xunit;

namespace WorkflowFramework.Tests.Scheduling;

public class InMemorySchedulerTests : IDisposable
{
    private readonly WorkflowRegistry _registry;
    private readonly InMemoryWorkflowScheduler _scheduler;

    public InMemorySchedulerTests()
    {
        _registry = new WorkflowRegistry();
        _registry.Register("test", () => Workflow.Create("test")
            .Step("step1", ctx => { ctx.Properties["executed"] = true; return Task.CompletedTask; })
            .Build());
        _scheduler = new InMemoryWorkflowScheduler(_registry);
    }

    [Fact]
    public async Task Schedule_ReturnsId()
    {
        var id = await _scheduler.ScheduleAsync("test", DateTimeOffset.UtcNow.AddHours(1), new WorkflowContext());
        id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Cancel_ExistingSchedule_ReturnsTrue()
    {
        var id = await _scheduler.ScheduleAsync("test", DateTimeOffset.UtcNow.AddHours(1), new WorkflowContext());
        var result = await _scheduler.CancelAsync(id);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_NonExistent_ReturnsFalse()
    {
        var result = await _scheduler.CancelAsync("nonexistent");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetPending_ReturnsScheduled()
    {
        await _scheduler.ScheduleAsync("test", DateTimeOffset.UtcNow.AddHours(1), new WorkflowContext());
        var pending = await _scheduler.GetPendingAsync();
        pending.Should().HaveCount(1);
        pending[0].WorkflowName.Should().Be("test");
    }

    [Fact]
    public async Task TickAsync_ExecutesDueWorkflows()
    {
        await _scheduler.ScheduleAsync("test", DateTimeOffset.UtcNow.AddSeconds(-1), new WorkflowContext());
        await _scheduler.TickAsync();
        _scheduler.ExecutedCount.Should().Be(1);
    }

    [Fact]
    public async Task TickAsync_DoesNotExecuteFutureWorkflows()
    {
        await _scheduler.ScheduleAsync("test", DateTimeOffset.UtcNow.AddHours(1), new WorkflowContext());
        await _scheduler.TickAsync();
        _scheduler.ExecutedCount.Should().Be(0);
    }

    [Fact]
    public async Task ScheduleCron_ReturnsId()
    {
        var id = await _scheduler.ScheduleCronAsync("test", "0 * * * *", () => new WorkflowContext());
        id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ScheduleCron_ShowsInPending()
    {
        await _scheduler.ScheduleCronAsync("test", "0 * * * *", () => new WorkflowContext());
        var pending = await _scheduler.GetPendingAsync();
        pending.Should().HaveCount(1);
        pending[0].IsRecurring.Should().BeTrue();
        pending[0].CronExpression.Should().Be("0 * * * *");
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        _scheduler.Dispose();
    }

    public void Dispose() => _scheduler.Dispose();
}

public class CronParserTests
{
    [Fact]
    public void GetNextOccurrence_EveryMinute()
    {
        var after = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var next = CronParser.GetNextOccurrence("* * * * *", after);
        next.Should().NotBeNull();
        next!.Value.Should().Be(new DateTimeOffset(2024, 1, 1, 12, 1, 0, TimeSpan.Zero));
    }

    [Fact]
    public void GetNextOccurrence_InvalidFormat_Throws()
    {
        var act = () => CronParser.GetNextOccurrence("bad", DateTimeOffset.UtcNow);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void GetNextOccurrence_SpecificMinute()
    {
        var after = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var next = CronParser.GetNextOccurrence("30 * * * *", after);
        next.Should().NotBeNull();
        next!.Value.Minute.Should().Be(30);
    }

    [Theory]
    [InlineData("*/5 * * * *")]
    [InlineData("0 0 * * *")]
    [InlineData("0 0 1 * *")]
    [InlineData("0 0 * * 0")]
    public void GetNextOccurrence_ValidExpressions_ReturnValue(string cron)
    {
        var next = CronParser.GetNextOccurrence(cron, DateTimeOffset.UtcNow);
        next.Should().NotBeNull();
    }

    [Fact]
    public void GetNextOccurrence_Range()
    {
        var after = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var next = CronParser.GetNextOccurrence("1-5 * * * *", after);
        next.Should().NotBeNull();
        next!.Value.Minute.Should().BeInRange(1, 5);
    }
}

public class ApprovalServiceTests
{
    [Fact]
    public async Task Approve_ResolvesTrue()
    {
        var service = new InMemoryApprovalService();
        var config = new ApprovalConfig { Name = "test" };
        var task = service.RequestApprovalAsync("wf1", config);
        service.Approve("wf1");
        var result = await task;
        result.Should().BeTrue();
    }

    [Fact]
    public async Task Reject_ResolvesFalse()
    {
        var service = new InMemoryApprovalService();
        var config = new ApprovalConfig { Name = "test" };
        var task = service.RequestApprovalAsync("wf1", config);
        service.Reject("wf1");
        var result = await task;
        result.Should().BeFalse();
    }

    [Fact]
    public void ApprovalConfig_Defaults()
    {
        var config = new ApprovalConfig();
        config.Name.Should().Be("Approval");
        config.Timeout.Should().BeNull();
        config.EscalationTimeout.Should().BeNull();
    }

    [Fact]
    public void ScheduledWorkflow_Defaults()
    {
        var sw = new ScheduledWorkflow();
        sw.Id.Should().BeEmpty();
        sw.WorkflowName.Should().BeEmpty();
        sw.ExecuteAt.Should().BeNull();
        sw.CronExpression.Should().BeNull();
        sw.IsRecurring.Should().BeFalse();
    }
}
