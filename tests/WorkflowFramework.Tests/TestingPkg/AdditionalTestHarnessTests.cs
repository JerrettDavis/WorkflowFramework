using FluentAssertions;
using WorkflowFramework.Builder;
using WorkflowFramework.Testing;
using Xunit;

namespace WorkflowFramework.Tests.TestingPkg;

public class AdditionalTestHarnessTests
{
    // StepTestBuilder coverage
    [Fact]
    public async Task StepTestBuilder_ExecuteTypedStep_ReturnsTypedContext()
    {
        var builder = new StepTestBuilder()
            .WithProperty("extra", "data");

        var step = new FakeStep<OrderData>("test", ctx =>
        {
            ctx.Data.Items.Add("item1");
            return Task.CompletedTask;
        });

        var ctx = await builder.ExecuteAsync(step, new OrderData());
        ctx.Data.Items.Should().Contain("item1");
        ctx.Properties["extra"].Should().Be("data");
    }

    // WorkflowTestHarness typed workflow
    [Fact]
    public async Task WorkflowTestHarness_ExecuteTypedWorkflow_NoOverrides()
    {
        var wf = Workflow.Create<OrderData>("typed")
            .Step("set", ctx => { ctx.Data.Items.Add("done"); return Task.CompletedTask; })
            .Build();

        var harness = new WorkflowTestHarness();
        var result = await harness.ExecuteAsync(wf, new OrderData());
        result.IsSuccess.Should().BeTrue();
        result.Data.Items.Should().Contain("done");
    }

    [Fact]
    public async Task WorkflowTestHarness_ExecuteTypedWorkflow_WithOverrides_FallsBackToDirectExecution()
    {
        // TypedWorkflowAdapter doesn't implement IWorkflow, so overrides fall through
        // to direct execution (no override applied). This tests that code path.
        var wf = Workflow.Create<OrderData>("typed")
            .Step("original", ctx => { ctx.Data.Items.Add("original"); return Task.CompletedTask; })
            .Build();

        var harness = new WorkflowTestHarness();
        harness.OverrideStep("original", ctx => Task.CompletedTask);
        var data = new OrderData();
        var result = await harness.ExecuteAsync(wf, data);
        result.IsSuccess.Should().BeTrue();
        // Override is NOT applied because TypedWorkflowAdapter doesn't implement IWorkflow
        data.Items.Should().Contain("original");
    }

    // WorkflowTestBuilder with step override and cancellation
    [Fact]
    public async Task WorkflowTestBuilder_WithStepOverride_OverridesCorrectly()
    {
        var wf = Workflow.Create("test")
            .Step("s1", ctx => { ctx.Properties["who"] = "original"; return Task.CompletedTask; })
            .Build();

        var fakeStep = new FakeStep("s1", ctx => { ctx.Properties["who"] = "fake"; return Task.CompletedTask; });
        var result = await new WorkflowTestBuilder()
            .WithStepOverride("s1", fakeStep)
            .ExecuteAsync(wf);

        result.Context.Properties["who"].Should().Be("fake");
    }

    [Fact]
    public async Task WorkflowTestBuilder_WithCancellation()
    {
        var cts = new CancellationTokenSource();
        var wf = Workflow.Create("test")
            .Step("s1", ctx =>
            {
                ctx.CancellationToken.Should().Be(cts.Token);
                return Task.CompletedTask;
            })
            .Build();

        await new WorkflowTestBuilder()
            .WithCancellation(cts.Token)
            .ExecuteAsync(wf);
    }

    // WorkflowAssertions missing paths
    [Fact]
    public void ShouldBeCompleted_WhenFaulted_Throws()
    {
        var result = new WorkflowResult(WorkflowStatus.Faulted, new WorkflowContext());
        var act = () => result.ShouldBeCompleted();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Faulted*");
    }

    [Fact]
    public void ShouldBeFaulted_WhenFaulted_Returns()
    {
        var result = new WorkflowResult(WorkflowStatus.Faulted, new WorkflowContext());
        result.ShouldBeFaulted().Should().BeSameAs(result);
    }

    [Fact]
    public void ShouldBeCompensated_WhenCompensated_Returns()
    {
        var result = new WorkflowResult(WorkflowStatus.Compensated, new WorkflowContext());
        result.ShouldBeCompensated().Should().BeSameAs(result);
    }

    [Fact]
    public void ShouldBeCompensated_WhenCompleted_Throws()
    {
        var result = new WorkflowResult(WorkflowStatus.Completed, new WorkflowContext());
        var act = () => result.ShouldBeCompensated();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Compensated*Completed*");
    }

    [Fact]
    public void ShouldHaveProperty_WithValue_Mismatch_Throws()
    {
        var ctx = new WorkflowContext();
        ctx.Properties["key"] = "actual";
        var result = new WorkflowResult(WorkflowStatus.Completed, ctx);
        var act = () => result.ShouldHaveProperty("key", "expected");
        act.Should().Throw<InvalidOperationException>().WithMessage("*expected*actual*");
    }

    [Fact]
    public void ShouldHaveProperty_WithValue_Missing_Throws()
    {
        var result = new WorkflowResult(WorkflowStatus.Completed, new WorkflowContext());
        var act = () => result.ShouldHaveProperty("missing", "val");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ShouldHaveNoErrors_WithErrors_Throws()
    {
        var ctx = new WorkflowContext();
        ctx.Errors.Add(new WorkflowError("step", new Exception("msg"), DateTimeOffset.UtcNow));
        var result = new WorkflowResult(WorkflowStatus.Faulted, ctx);
        var act = () => result.ShouldHaveNoErrors();
        act.Should().Throw<InvalidOperationException>().WithMessage("*1*");
    }

    // FakeStep<T> no-op action path
    [Fact]
    public async Task FakeStepTyped_NoAction_ExecutesWithoutError()
    {
        var step = new FakeStep<OrderData>("test");
        step.ExecutionCount.Should().Be(0);
        var ctx = new WorkflowContext<OrderData>(new OrderData());
        await step.ExecuteAsync(ctx);
        step.ExecutionCount.Should().Be(1);
    }

    private class OrderData
    {
        public List<string> Items { get; set; } = new();
    }
}
