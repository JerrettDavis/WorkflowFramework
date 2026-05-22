using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Composition;

namespace WorkflowFramework.Tests.TinyBDD.Integration;

[Feature("Scatter gather step")]
public class ScatterGatherStepTests : TinyBddTestBase
{
    public ScatterGatherStepTests(ITestOutputHelper output) : base(output) { }

    [Scenario("All branches execute and aggregator receives their results"), Fact]
    public async Task AllBranchesRunAndAggregate()
    {
        var aggregatedResults = new List<object?>();

        var handler1 = Substitute.For<IStep>();
        handler1.Name.Returns("h1");
        handler1.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci =>
            {
                ((IWorkflowContext)ci[0]).Properties["__Result_h1"] = "result1";
                return Task.CompletedTask;
            });

        var handler2 = Substitute.For<IStep>();
        handler2.Name.Returns("h2");
        handler2.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci =>
            {
                ((IWorkflowContext)ci[0]).Properties["__Result_h2"] = "result2";
                return Task.CompletedTask;
            });

        var step = new ScatterGatherStep(
            new[] { handler1, handler2 },
            (results, _) => { aggregatedResults.AddRange(results); return Task.CompletedTask; },
            TimeSpan.FromSeconds(5));

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        await Given("context and aggregated results after scatter-gather with two handlers", () => (context, aggregatedResults))
            .Then("the aggregator received two results and the results key is set", state =>
            {
                state.context.Properties.Should().ContainKey(ScatterGatherStep.ResultsKey);
                state.aggregatedResults.Should().HaveCount(2);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ScatterGather with one handler stores result under ResultsKey"), Fact]
    public async Task SingleHandlerResultIsStored()
    {
        var handler = Substitute.For<IStep>();
        handler.Name.Returns("solo");
        handler.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci =>
            {
                ((IWorkflowContext)ci[0]).Properties["__Result_solo"] = 42;
                return Task.CompletedTask;
            });

        var step = new ScatterGatherStep(
            new[] { handler },
            (results, ctx) => { ctx.Properties["aggregated"] = results[0]; return Task.CompletedTask; },
            TimeSpan.FromSeconds(5));

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        await Given("context after scatter-gather with a single handler producing 42", () => context)
            .Then("the aggregated property is 42", ctx =>
            {
                ctx.Properties["aggregated"].Should().Be(42);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Failing branch does not prevent other branches from running"), Fact]
    public async Task FailingBranchDoesNotBlockOthers()
    {
        var faultingHandler = Substitute.For<IStep>();
        faultingHandler.Name.Returns("faulting");
        faultingHandler.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns<Task>(_ => throw new InvalidOperationException("branch error"));

        var goodHandler = Substitute.For<IStep>();
        goodHandler.Name.Returns("good");
        goodHandler.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci =>
            {
                ((IWorkflowContext)ci[0]).Properties["__Result_good"] = "ok";
                return Task.CompletedTask;
            });

        var step = new ScatterGatherStep(
            new[] { faultingHandler, goodHandler },
            (_, _) => Task.CompletedTask,
            TimeSpan.FromSeconds(5));

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        await Given("context after scatter-gather where one branch threw", () => context)
            .Then("the step completes and the results key is present", ctx =>
            {
                ctx.Properties.Should().ContainKey(ScatterGatherStep.ResultsKey);
                return true;
            })
            .AssertPassed();
    }
}
