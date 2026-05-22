using FluentAssertions;
using NSubstitute;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using WorkflowFramework.Extensions.Scheduling;
using WorkflowFramework.Registry;

namespace WorkflowFramework.Extensions.Scheduling.Tests.Scheduling;

[Feature("InMemoryWorkflowScheduler — in-memory workflow scheduling")]
public class InMemoryWorkflowSchedulerScenarios : TinyBddXunitBase
{
    public InMemoryWorkflowSchedulerScenarios(ITestOutputHelper output) : base(output) { }

    private static IWorkflowRegistry MakeRegistry()
    {
        var reg = Substitute.For<IWorkflowRegistry>();
        var wf = Substitute.For<IWorkflow>();
        var ctx = Substitute.For<IWorkflowContext>();
        ctx.Errors.Returns(new List<WorkflowError>());
        var result = new WorkflowResult(WorkflowStatus.Completed, ctx);
        wf.ExecuteAsync(Arg.Any<IWorkflowContext>()).Returns(result);
        reg.Resolve(Arg.Any<string>()).Returns(wf);
        return reg;
    }

    private static IWorkflowContext MakeContext()
    {
        var ctx = Substitute.For<IWorkflowContext>();
        ctx.WorkflowId.Returns(Guid.NewGuid().ToString());
        ctx.CancellationToken.Returns(CancellationToken.None);
        ctx.Properties.Returns(new Dictionary<string, object?>());
        return ctx;
    }

    [Scenario("ScheduleAsync returns a non-empty schedule id"), Fact]
    public async Task ScheduleAsyncReturnsId()
    {
        using var scheduler = new InMemoryWorkflowScheduler(MakeRegistry());
        var scheduleId = await scheduler.ScheduleAsync("MyWorkflow", DateTimeOffset.UtcNow.AddHours(1), MakeContext());

        await Given("a workflow scheduled for 1 hour from now", () => scheduleId)
            .Then("returned schedule id is non-empty", id =>
            {
                id.Should().NotBeNullOrEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("GetPendingAsync returns the scheduled workflow"), Fact]
    public async Task GetPendingReturnsScheduledWorkflow()
    {
        using var scheduler = new InMemoryWorkflowScheduler(MakeRegistry());
        await scheduler.ScheduleAsync("OrderFlow", DateTimeOffset.UtcNow.AddHours(1), MakeContext());
        var pending = await scheduler.GetPendingAsync();

        await Given("'OrderFlow' scheduled for the future", () => pending)
            .Then("GetPendingAsync contains the scheduled workflow", p =>
            {
                p.Should().ContainSingle(s => s.WorkflowName == "OrderFlow");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CancelAsync removes scheduled workflow"), Fact]
    public async Task CancelRemovesSchedule()
    {
        using var scheduler = new InMemoryWorkflowScheduler(MakeRegistry());
        var id = await scheduler.ScheduleAsync("ToBeCancelled", DateTimeOffset.UtcNow.AddHours(1), MakeContext());
        var cancelled = await scheduler.CancelAsync(id);
        var pending = await scheduler.GetPendingAsync();

        await Given("a schedule that was then cancelled", () => (cancelled, pending))
            .Then("CancelAsync returned true and schedule is no longer pending", t =>
            {
                t.cancelled.Should().BeTrue();
                t.pending.Should().BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CancelAsync returns false for unknown schedule id"), Fact]
    public async Task CancelReturnsFalseForUnknown()
    {
        using var scheduler = new InMemoryWorkflowScheduler(MakeRegistry());
        var result = await scheduler.CancelAsync("does-not-exist");

        await Given("CancelAsync called with unknown id", () => result)
            .Then("result is false", r =>
            {
                r.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("TickAsync executes due workflows"), Fact]
    public async Task TickAsyncExecutesDueWorkflows()
    {
        var registry = MakeRegistry();
        using var scheduler = new InMemoryWorkflowScheduler(registry);

        // Schedule a workflow in the past (due immediately)
        await scheduler.ScheduleAsync("PastWorkflow", DateTimeOffset.UtcNow.AddSeconds(-1), MakeContext());
        await scheduler.TickAsync();

        await Given("a workflow scheduled in the past then TickAsync called", () => scheduler)
            .Then("executed count is 1", s =>
            {
                s.ExecutedCount.Should().Be(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("TickAsync removes executed one-time schedules"), Fact]
    public async Task TickAsyncRemovesExecutedSchedule()
    {
        using var scheduler = new InMemoryWorkflowScheduler(MakeRegistry());
        await scheduler.ScheduleAsync("OneTime", DateTimeOffset.UtcNow.AddSeconds(-1), MakeContext());
        await scheduler.TickAsync();

        var pending = await scheduler.GetPendingAsync();

        await Given("a one-time schedule that was ticked", () => pending)
            .Then("schedule is removed after execution", p =>
            {
                p.Should().BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ScheduleCronAsync creates a recurring schedule"), Fact]
    public async Task ScheduleCronAsyncCreatesRecurring()
    {
        using var scheduler = new InMemoryWorkflowScheduler(MakeRegistry());
        await scheduler.ScheduleCronAsync("RecurringFlow", "* * * * *", MakeContext);
        var pending = await scheduler.GetPendingAsync();

        await Given("a workflow scheduled via cron expression '* * * * *'", () => pending)
            .Then("pending contains the recurring schedule", p =>
            {
                p.Should().ContainSingle(s => s.WorkflowName == "RecurringFlow" && s.IsRecurring);
                return true;
            })
            .AssertPassed();
    }
}
