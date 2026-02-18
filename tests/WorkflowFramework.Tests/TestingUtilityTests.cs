using FluentAssertions;
using WorkflowFramework.Testing;
using Xunit;

namespace WorkflowFramework.Tests;

public class TestingUtilityTests
{
    [Fact]
    public async Task FakeStep_TracksExecutions()
    {
        var fake = new FakeStep("TestStep", ctx =>
        {
            ctx.Properties["Ran"] = true;
            return Task.CompletedTask;
        });

        var context = new WorkflowContext();
        await fake.ExecuteAsync(context);
        await fake.ExecuteAsync(context);

        fake.ExecutionCount.Should().Be(2);
        fake.ExecutionContexts.Should().HaveCount(2);
        context.Properties["Ran"].Should().Be(true);
    }

    [Fact]
    public async Task InMemoryWorkflowEvents_CapturesAll()
    {
        var events = new InMemoryWorkflowEvents();
        var workflow = Workflow.Create("Test")
            .WithEvents(events)
            .Step("A", _ => Task.CompletedTask)
            .Step("B", _ => Task.CompletedTask)
            .Build();

        await workflow.ExecuteAsync(new WorkflowContext());

        events.WorkflowStarted.Should().HaveCount(1);
        events.WorkflowCompleted.Should().HaveCount(1);
        events.StepStarted.Should().HaveCount(2);
        events.StepCompleted.Should().HaveCount(2);
        events.WorkflowFailed.Should().BeEmpty();
    }

    [Fact]
    public async Task WorkflowTestHarness_OverridesStep()
    {
        var workflow = Workflow.Create("Test")
            .Step("Original", ctx =>
            {
                ctx.Properties["Source"] = "original";
                return Task.CompletedTask;
            })
            .Build();

        var harness = new WorkflowTestHarness()
            .OverrideStep("Original", ctx =>
            {
                ctx.Properties["Source"] = "overridden";
                return Task.CompletedTask;
            });

        var context = new WorkflowContext();
        await harness.ExecuteAsync(workflow, context);

        context.Properties["Source"].Should().Be("overridden");
    }

    [Fact]
    public async Task StepTestBuilder_ExecutesStepInIsolation()
    {
        var step = new FakeStep("TestStep", ctx =>
        {
            var input = (string)ctx.Properties["Input"]!;
            ctx.Properties["Output"] = input.ToUpper();
            return Task.CompletedTask;
        });

        var context = await new StepTestBuilder()
            .WithProperty("Input", "hello")
            .ExecuteAsync(step);

        context.Properties["Output"].Should().Be("HELLO");
    }

    [Fact]
    public async Task FakeStep_NoOp_Works()
    {
        var fake = new FakeStep("NoOp");
        await fake.ExecuteAsync(new WorkflowContext());
        fake.ExecutionCount.Should().Be(1);
    }
}
