using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Composition;

namespace WorkflowFramework.Tests.TinyBDD.Integration;

// Phase 3 — updated to use the new typed-recipient API (ScatterGatherStep.Recipient).
// The previous IEnumerable<IStep>-based tests have been migrated to typed recipients
// that return results directly, eliminating the shared-context __Result_{name} mutation pattern.
// See .plan/patternkit-iteration-2.md §6.

[Feature("Scatter gather step")]
public class ScatterGatherStepTests : TinyBddTestBase
{
    public ScatterGatherStepTests(ITestOutputHelper output) : base(output) { }

    private static ScatterGatherStep.Recipient Recipient(string name, object? result)
        => new(name, (_, _) => new ValueTask<object?>(result));

    [Scenario("All branches execute and aggregator receives their results"), Fact]
    public async Task AllBranchesRunAndAggregate()
    {
        // Phase 3: branches are typed recipients returning values directly.
        // No shared-context mutation (__Result_h1, __Result_h2) required.
        var aggregatedResults = new List<object?>();

        var step = new ScatterGatherStep(
            new[]
            {
                Recipient("h1", "result1"),
                Recipient("h2", "result2"),
            },
            (results, _) => { aggregatedResults.AddRange(results); return Task.CompletedTask; },
            TimeSpan.FromSeconds(5));

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        await Given("context and aggregated results after scatter-gather with two branches", () => (context, aggregatedResults))
            .Then("the aggregator received two results and the results key is set", state =>
            {
                state.context.Properties.Should().ContainKey(ScatterGatherStep.ResultsKey);
                state.aggregatedResults.Should().HaveCount(2);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ScatterGather with one branch stores result under ResultsKey"), Fact]
    public async Task SingleHandlerResultIsStored()
    {
        // Phase 3: single typed recipient returning 42.
        var step = new ScatterGatherStep(
            new[] { Recipient("solo", 42) },
            (results, ctx) => { ctx.Properties["aggregated"] = results[0]; return Task.CompletedTask; },
            TimeSpan.FromSeconds(5));

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        await Given("context after scatter-gather with a single branch producing 42", () => context)
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
        // Phase 3: faulting recipient produces a failure envelope; good recipient still runs.
        var step = new ScatterGatherStep(
            new[]
            {
                new ScatterGatherStep.Recipient("faulting", (_, _) =>
                    ValueTask.FromException<object?>(new InvalidOperationException("branch error"))),
                Recipient("good", "ok"),
            },
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
