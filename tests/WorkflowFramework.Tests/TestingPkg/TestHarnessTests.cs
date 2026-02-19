using FluentAssertions;
using WorkflowFramework.Builder;
using WorkflowFramework.Testing;
using Xunit;

namespace WorkflowFramework.Tests.TestingPkg;

public class TestHarnessTests
{
    [Fact]
    public async Task WorkflowTestHarness_ExecutesWithoutOverrides()
    {
        var workflow = new WorkflowBuilder()
            .WithName("Test")
            .Step("s1", ctx => { ctx.Properties["ran"] = true; return Task.CompletedTask; })
            .Build();
        var harness = new WorkflowTestHarness();
        var context = new WorkflowContext();
        var result = await harness.ExecuteAsync(workflow, context);
        result.IsSuccess.Should().BeTrue();
        context.Properties["ran"].Should().Be(true);
    }

    [Fact]
    public async Task WorkflowTestHarness_OverridesStep()
    {
        var workflow = new WorkflowBuilder()
            .WithName("Test")
            .Step("original", ctx => { ctx.Properties["who"] = "original"; return Task.CompletedTask; })
            .Build();
        var harness = new WorkflowTestHarness();
        harness.OverrideStep("original", ctx => { ctx.Properties["who"] = "override"; return Task.CompletedTask; });
        var context = new WorkflowContext();
        await harness.ExecuteAsync(workflow, context);
        context.Properties["who"].Should().Be("override");
    }

    [Fact]
    public async Task WorkflowTestHarness_OverrideWithStep()
    {
        var workflow = new WorkflowBuilder()
            .WithName("Test")
            .Step("s1", ctx => Task.CompletedTask)
            .Build();
        var fakeStep = new FakeStep("s1", ctx => { ctx.Properties["fake"] = true; return Task.CompletedTask; });
        var harness = new WorkflowTestHarness();
        harness.OverrideStep("s1", fakeStep);
        var context = new WorkflowContext();
        await harness.ExecuteAsync(workflow, context);
        fakeStep.ExecutionCount.Should().Be(1);
        context.Properties["fake"].Should().Be(true);
    }

    [Fact]
    public void FakeStep_TracksExecutionCount()
    {
        var fake = new FakeStep("test");
        fake.ExecutionCount.Should().Be(0);
        fake.ExecuteAsync(new WorkflowContext()).Wait();
        fake.ExecutionCount.Should().Be(1);
    }

    [Fact]
    public async Task FakeStep_TracksContexts()
    {
        var fake = new FakeStep("test");
        var ctx1 = new WorkflowContext();
        var ctx2 = new WorkflowContext();
        await fake.ExecuteAsync(ctx1);
        await fake.ExecuteAsync(ctx2);
        fake.ExecutionContexts.Should().HaveCount(2);
    }

    [Fact]
    public void MockStep_RecordsInvocations()
    {
        var mock = new MockStep("test");
        mock.InvocationCount.Should().Be(0);
        mock.ExecuteAsync(new WorkflowContext()).Wait();
        mock.InvocationCount.Should().Be(1);
        mock.Invocations.Should().HaveCount(1);
    }

    [Fact]
    public async Task MockStep_ThrowsConfiguredException()
    {
        var mock = new MockStep("test", throwException: new InvalidOperationException("fail"));
        var act = () => mock.ExecuteAsync(new WorkflowContext());
        await act.Should().ThrowAsync<InvalidOperationException>();
        mock.InvocationCount.Should().Be(1);
    }

    [Fact]
    public async Task MockStep_ExecutesAction()
    {
        var executed = false;
        var mock = new MockStep("test", action: ctx => { executed = true; return Task.CompletedTask; });
        await mock.ExecuteAsync(new WorkflowContext());
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task StepTestBuilder_SetsPropertiesAndExecutes()
    {
        var builder = new StepTestBuilder()
            .WithProperty("key", "value");
        var step = new TestStep("test", ctx =>
        {
            ctx.Properties["key"].Should().Be("value");
            return Task.CompletedTask;
        });
        var context = await builder.ExecuteAsync(step);
        context.Properties["key"].Should().Be("value");
    }

    [Fact]
    public async Task StepTestBuilder_SupportsCancellation()
    {
        var cts = new CancellationTokenSource();
        var builder = new StepTestBuilder().WithCancellation(cts.Token);
        var step = new TestStep("test", ctx =>
        {
            ctx.CancellationToken.Should().Be(cts.Token);
            return Task.CompletedTask;
        });
        await builder.ExecuteAsync(step);
    }

    [Fact]
    public async Task WorkflowTestBuilder_FullPipeline()
    {
        var workflow = new WorkflowBuilder()
            .WithName("Test")
            .Step("s1", ctx => { ctx.Properties["result"] = "done"; return Task.CompletedTask; })
            .Build();
        var result = await new WorkflowTestBuilder()
            .WithProperty("input", 42)
            .ExecuteAsync(workflow);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task WorkflowAssertions_ShouldBeCompleted()
    {
        var workflow = new WorkflowBuilder().WithName("Test").Build();
        var result = await workflow.ExecuteAsync(new WorkflowContext());
        result.ShouldBeCompleted();
    }

    [Fact]
    public void WorkflowAssertions_ShouldBeFaulted_ThrowsOnCompleted()
    {
        var result = new WorkflowResult(WorkflowStatus.Completed, new WorkflowContext());
        var act = () => result.ShouldBeFaulted();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void WorkflowAssertions_ShouldHaveProperty()
    {
        var context = new WorkflowContext();
        context.Properties["key"] = "val";
        var result = new WorkflowResult(WorkflowStatus.Completed, context);
        result.ShouldHaveProperty("key");
        result.ShouldHaveProperty("key", "val");
    }

    [Fact]
    public void WorkflowAssertions_ShouldHaveProperty_Missing_Throws()
    {
        var result = new WorkflowResult(WorkflowStatus.Completed, new WorkflowContext());
        var act = () => result.ShouldHaveProperty("missing");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void WorkflowAssertions_ShouldHaveNoErrors()
    {
        var result = new WorkflowResult(WorkflowStatus.Completed, new WorkflowContext());
        result.ShouldHaveNoErrors();
    }

    [Fact]
    public async Task InMemoryWorkflowEvents_CapturesEvents()
    {
        var events = new InMemoryWorkflowEvents();
        var context = new WorkflowContext();
        var step = new TestStep("test");
        await events.OnWorkflowStartedAsync(context);
        await events.OnStepStartedAsync(context, step);
        await events.OnStepCompletedAsync(context, step);
        await events.OnWorkflowCompletedAsync(context);
        events.WorkflowStarted.Should().HaveCount(1);
        events.StepStarted.Should().HaveCount(1);
        events.StepCompleted.Should().HaveCount(1);
        events.WorkflowCompleted.Should().HaveCount(1);
    }

    [Fact]
    public async Task InMemoryWorkflowEvents_CapturesFailures()
    {
        var events = new InMemoryWorkflowEvents();
        var context = new WorkflowContext();
        var step = new TestStep("test");
        var ex = new Exception("fail");
        await events.OnStepFailedAsync(context, step, ex);
        await events.OnWorkflowFailedAsync(context, ex);
        events.StepFailed.Should().HaveCount(1);
        events.WorkflowFailed.Should().HaveCount(1);
    }

    private sealed class TestStep(string name, Func<IWorkflowContext, Task>? action = null) : IStep
    {
        public string Name { get; } = name;
        public Task ExecuteAsync(IWorkflowContext context) => action?.Invoke(context) ?? Task.CompletedTask;
    }
}
