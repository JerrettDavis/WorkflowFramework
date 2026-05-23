using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Composition;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Composition;

[Feature("ScatterGatherStep — characterization (Phase G.2)")]
public class ScatterGatherStepScenarios : TinyBddTestBase
{
    public ScatterGatherStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("ScatterGatherStep Name returns 'ScatterGather'"), Fact]
    public async Task NameIsScatterGather()
    {
        var sut = new ScatterGatherStep(
            Array.Empty<IStep>(),
            (_, _) => Task.CompletedTask,
            TimeSpan.FromSeconds(5));

        await Given("ScatterGatherStep instance", () => sut)
            .Then("Name is 'ScatterGather'", s =>
            {
                s.Name.Should().Be("ScatterGather");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("All handlers execute and aggregator receives their results"), Fact]
    public async Task AllHandlersRunAndAggregatorReceivesResults()
    {
        var aggregatedResults = new List<object?>();

        var h1 = Substitute.For<IStep>();
        h1.Name.Returns("h1");
        h1.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci => { ((IWorkflowContext)ci[0]).Properties["__Result_h1"] = "result1"; return Task.CompletedTask; });

        var h2 = Substitute.For<IStep>();
        h2.Name.Returns("h2");
        h2.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci => { ((IWorkflowContext)ci[0]).Properties["__Result_h2"] = "result2"; return Task.CompletedTask; });

        var sut = new ScatterGatherStep(
            new[] { h1, h2 },
            (results, _) => { aggregatedResults.AddRange(results); return Task.CompletedTask; },
            TimeSpan.FromSeconds(5));

        var ctx = new WorkflowContext();
        await sut.ExecuteAsync(ctx);

        await Given("aggregated results after scatter-gather with two handlers", () => (aggregatedResults, ctx))
            .Then("two results were collected and ResultsKey is set on context", state =>
            {
                state.aggregatedResults.Should().HaveCount(2);
                state.ctx.Properties.Should().ContainKey(ScatterGatherStep.ResultsKey);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Failing handler does not prevent aggregator from being called"), Fact]
    public async Task FailingHandlerDoesNotBlockAggregator()
    {
        var aggregatorCalled = false;

        var faulting = Substitute.For<IStep>();
        faulting.Name.Returns("bad");
        faulting.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns<Task>(_ => throw new InvalidOperationException("branch failure"));

        var good = Substitute.For<IStep>();
        good.Name.Returns("good");
        good.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci => { ((IWorkflowContext)ci[0]).Properties["__Result_good"] = "ok"; return Task.CompletedTask; });

        var sut = new ScatterGatherStep(
            new[] { faulting, good },
            (_, _) => { aggregatorCalled = true; return Task.CompletedTask; },
            TimeSpan.FromSeconds(5));

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("whether aggregator was called despite faulting handler", () => aggregatorCalled)
            .Then("aggregator was still called", called =>
            {
                called.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ResultsKey constant has expected value"), Fact]
    public async Task ResultsKeyHasExpectedValue()
    {
        await Given("ScatterGatherStep.ResultsKey", () => ScatterGatherStep.ResultsKey)
            .Then("it equals '__ScatterGatherResults'", key =>
            {
                key.Should().Be("__ScatterGatherResults");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null handlers throws ArgumentNullException"), Fact]
    public async Task NullHandlersThrows()
    {
        Exception? caught = null;
        try { _ = new ScatterGatherStep(null!, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1)); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null handlers", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null aggregator throws ArgumentNullException"), Fact]
    public async Task NullAggregatorThrows()
    {
        Exception? caught = null;
        try { _ = new ScatterGatherStep(Array.Empty<IStep>(), null!, TimeSpan.FromSeconds(1)); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null aggregator", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Single handler result is stored under ResultsKey"), Fact]
    public async Task SingleHandlerResultStored()
    {
        var h = Substitute.For<IStep>();
        h.Name.Returns("solo");
        h.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci => { ((IWorkflowContext)ci[0]).Properties["__Result_solo"] = 42; return Task.CompletedTask; });

        var sut = new ScatterGatherStep(
            new[] { h },
            (results, ctx) => { ctx.Properties["aggregated"] = results[0]; return Task.CompletedTask; },
            TimeSpan.FromSeconds(5));

        var ctx = new WorkflowContext();
        await sut.ExecuteAsync(ctx);

        await Given("aggregated property after scatter-gather with solo handler", () => ctx)
            .Then("aggregated is 42", c =>
            {
                c.Properties["aggregated"].Should().Be(42);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Empty handlers list calls aggregator with empty results"), Fact]
    public async Task EmptyHandlersCallsAggregatorWithEmptyList()
    {
        IReadOnlyList<object?>? received = null;
        var sut = new ScatterGatherStep(
            Array.Empty<IStep>(),
            (results, _) => { received = results; return Task.CompletedTask; },
            TimeSpan.FromSeconds(5));

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("results received by aggregator with empty handler list", () => received)
            .Then("aggregator was called with empty list", r =>
            {
                r.Should().NotBeNull().And.BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Handler that throws OperationCanceledException is swallowed and returns null"), Fact]
    public async Task HandlerOperationCanceledException_IsSwallowed()
    {
        IReadOnlyList<object?>? received = null;

        var cancelling = Substitute.For<IStep>();
        cancelling.Name.Returns("cancelling");
        cancelling.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns<Task>(_ => Task.FromException(new OperationCanceledException()));

        var sut = new ScatterGatherStep(
            new[] { cancelling },
            (results, _) => { received = results; return Task.CompletedTask; },
            TimeSpan.FromSeconds(5));

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("results after handler throws OperationCanceledException", () => received)
            .Then("aggregator receives null result for cancelled handler", r =>
            {
                r.Should().NotBeNull().And.ContainSingle(v => v == null);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Timeout fires and aggregator receives partial results"), Fact]
    public async Task Timeout_AggregatorsReceivesPartialResults()
    {
        IReadOnlyList<object?>? received = null;

        var fast = Substitute.For<IStep>();
        fast.Name.Returns("fast");
        fast.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci =>
            {
                ((IWorkflowContext)ci[0]).Properties["__Result_fast"] = "done";
                return Task.CompletedTask;
            });

        var slow = Substitute.For<IStep>();
        slow.Name.Returns("slow");
        slow.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(async _ =>
            {
                // Delay longer than the ScatterGatherStep timeout (50ms) but bounded
                // so the task eventually completes and doesn't orphan the testhost.
                await Task.Delay(500).ConfigureAwait(false);
            });

        // Very short timeout to trigger partial results path.
        var sut = new ScatterGatherStep(
            new[] { fast, slow },
            (results, _) => { received = results; return Task.CompletedTask; },
            TimeSpan.FromMilliseconds(50));

        var ctx = new WorkflowContext();
        await sut.ExecuteAsync(ctx);

        await Given("partial results after scatter-gather timeout", () => received)
            .Then("aggregator received partial results (not null, from completed handlers)", r =>
            {
                r.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }
}
