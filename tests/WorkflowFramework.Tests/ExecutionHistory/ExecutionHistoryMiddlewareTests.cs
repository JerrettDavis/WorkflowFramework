using FluentAssertions;
using WorkflowFramework.Builder;
using WorkflowFramework.Extensions.Diagnostics.ExecutionHistory;
using Xunit;

namespace WorkflowFramework.Tests.ExecutionHistory;

public class ExecutionHistoryMiddlewareTests
{
    [Fact]
    public async Task Middleware_RecordsStepResults_OnSuccess()
    {
        var store = new InMemoryExecutionHistoryStore();

        var workflow = new WorkflowBuilder()
            .WithName("TestWorkflow")
            .Step("Step1", _ => Task.CompletedTask)
            .Step("Step2", _ => Task.CompletedTask)
            .WithExecutionHistory(store)
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.Status.Should().Be(WorkflowStatus.Completed);
        store.AllRecords.Should().ContainSingle();

        var record = store.AllRecords[0];
        record.Status.Should().Be(WorkflowStatus.Completed);
        record.StepResults.Should().HaveCount(2);
        record.StepResults[0].StepName.Should().Be("Step1");
        record.StepResults[0].Status.Should().Be(WorkflowStatus.Completed);
        record.StepResults[1].StepName.Should().Be("Step2");
        record.CompletedAt.Should().NotBeNull();
        record.Duration.Should().NotBeNull();
        record.Error.Should().BeNull();
    }

    [Fact]
    public async Task Middleware_RecordsStepResults_OnFailure()
    {
        var store = new InMemoryExecutionHistoryStore();

        var workflow = new WorkflowBuilder()
            .WithName("FailWorkflow")
            .Step("Good", _ => Task.CompletedTask)
            .Step("Bad", _ => throw new InvalidOperationException("boom"))
            .WithExecutionHistory(store)
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.Status.Should().Be(WorkflowStatus.Faulted);
        store.AllRecords.Should().ContainSingle();

        var record = store.AllRecords[0];
        record.Status.Should().Be(WorkflowStatus.Faulted);
        record.Error.Should().Be("boom");
        record.StepResults.Should().HaveCount(2);
        record.StepResults[0].Status.Should().Be(WorkflowStatus.Completed);
        record.StepResults[1].Status.Should().Be(WorkflowStatus.Faulted);
        record.StepResults[1].Error.Should().Be("boom");
    }

    [Fact]
    public async Task Middleware_CapturesPropertySnapshots()
    {
        var store = new InMemoryExecutionHistoryStore();

        var workflow = new WorkflowBuilder()
            .WithName("SnapshotWorkflow")
            .Step("SetValue", ctx =>
            {
                ctx.Properties["myKey"] = "hello";
                return Task.CompletedTask;
            })
            .WithExecutionHistory(store)
            .Build();

        await workflow.ExecuteAsync(new WorkflowContext());

        var step = store.AllRecords[0].StepResults[0];
        step.InputSnapshot.Should().NotContainKey("myKey");
        step.OutputSnapshot.Should().ContainKey("myKey").WhoseValue.Should().Be("hello");
    }

    [Fact]
    public async Task Middleware_StepTimingIsRecorded()
    {
        var store = new InMemoryExecutionHistoryStore();

        var workflow = new WorkflowBuilder()
            .WithName("TimingWorkflow")
            .Step("Slow", async _ => await Task.Delay(50))
            .WithExecutionHistory(store)
            .Build();

        await workflow.ExecuteAsync(new WorkflowContext());

        var step = store.AllRecords[0].StepResults[0];
        step.StartedAt.Should().BeBefore(step.CompletedAt!.Value);
        step.Duration.Should().BeGreaterThan(TimeSpan.FromMilliseconds(20));
    }

    [Fact]
    public async Task WithExecutionHistory_OutParam_ProvidesStore()
    {
        var builder = new WorkflowBuilder()
            .WithName("OutParamTest")
            .Step("A", _ => Task.CompletedTask)
            .WithExecutionHistory(out var store);

        var workflow = builder.Build();
        await workflow.ExecuteAsync(new WorkflowContext());

        store.AllRecords.Should().ContainSingle();
    }
}
