using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Scheduling;
using WorkflowFramework.Registry;

namespace WorkflowFramework.Tests.TinyBDD.Scheduling;

[Feature("InMemory workflow scheduler")]
public class InMemoryWorkflowSchedulerTests : TinyBddTestBase
{
    public InMemoryWorkflowSchedulerTests(ITestOutputHelper output) : base(output) { }

    private static InMemoryWorkflowScheduler BuildScheduler(IWorkflowRegistry? registry = null)
    {
        registry ??= Substitute.For<IWorkflowRegistry>();
        return new InMemoryWorkflowScheduler(registry);
    }

    [Scenario("ScheduleAsync returns a non-empty schedule ID"), Fact]
    public async Task ScheduleReturnsId()
    {
        var scheduler = BuildScheduler();
        var id = await scheduler.ScheduleAsync("wf1", DateTimeOffset.UtcNow.AddHours(1), new WorkflowContext());

        await Given("a schedule ID returned from ScheduleAsync", () => id)
            .Then("the ID is not empty", i =>
            {
                i.Should().NotBeNullOrEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("GetPendingAsync returns the scheduled entry"), Fact]
    public async Task ScheduledEntryAppearsInPending()
    {
        var scheduler = BuildScheduler();
        await scheduler.ScheduleAsync("my-workflow", DateTimeOffset.UtcNow.AddMinutes(5), new WorkflowContext());
        var pending = await scheduler.GetPendingAsync();

        await Given("the pending list after scheduling my-workflow", () => pending)
            .Then("there is one entry with the correct workflow name", list =>
            {
                list.Should().HaveCount(1);
                list[0].WorkflowName.Should().Be("my-workflow");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CancelAsync removes the schedule"), Fact]
    public async Task CancelRemovesSchedule()
    {
        var scheduler = BuildScheduler();
        var id = await scheduler.ScheduleAsync("wf-cancel", DateTimeOffset.UtcNow.AddHours(1), new WorkflowContext());
        var cancelled = await scheduler.CancelAsync(id);
        var pending = await scheduler.GetPendingAsync();

        await Given("state after cancel: result and pending list", () => (cancelled, pending))
            .Then("cancel returned true and pending is empty", state =>
            {
                state.cancelled.Should().BeTrue();
                state.pending.Should().BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CancelAsync for unknown ID returns false"), Fact]
    public async Task CancelUnknownReturnsFalse()
    {
        var scheduler = BuildScheduler();
        var result = await scheduler.CancelAsync("does-not-exist");

        await Given("the result of cancelling an unknown schedule ID", () => result)
            .Then("the result is false", r =>
            {
                r.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ScheduleCronAsync records a recurring entry"), Fact]
    public async Task ScheduleCronRecordsRecurringEntry()
    {
        var scheduler = BuildScheduler();
        await scheduler.ScheduleCronAsync("cron-wf", "0 * * * *", () => new WorkflowContext());
        var pending = await scheduler.GetPendingAsync();

        await Given("pending entries after cron schedule", () => pending)
            .Then("the entry is recurring with the correct cron expression", list =>
            {
                list.Should().HaveCount(1);
                list[0].IsRecurring.Should().BeTrue();
                list[0].CronExpression.Should().Be("0 * * * *");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("TickAsync executes a due workflow"), Fact]
    public async Task TickExecutesDueWorkflow()
    {
        var executed = false;
        var stubWorkflow = new TrackingWorkflow("tick-wf", () => executed = true);

        var registry = Substitute.For<IWorkflowRegistry>();
        registry.Resolve("tick-wf").Returns(stubWorkflow);

        var scheduler = new InMemoryWorkflowScheduler(registry);
        await scheduler.ScheduleAsync("tick-wf", DateTimeOffset.UtcNow.AddSeconds(-1), new WorkflowContext());
        await scheduler.TickAsync();

        await Given("state after ticking a scheduler with a past-due entry", () => (scheduler, executed))
            .Then("ExecutedCount is 1 and the workflow ran", state =>
            {
                state.scheduler.ExecutedCount.Should().Be(1);
                state.executed.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    private sealed class TrackingWorkflow : IWorkflow
    {
        private readonly Action _onExecute;
        public string Name { get; }
        public IReadOnlyList<IStep> Steps => Array.Empty<IStep>();

        public TrackingWorkflow(string name, Action onExecute)
        {
            Name = name;
            _onExecute = onExecute;
        }

        public Task<WorkflowResult> ExecuteAsync(IWorkflowContext context)
        {
            _onExecute();
            return Task.FromResult(new WorkflowResult(WorkflowStatus.Completed, context));
        }
    }
}
