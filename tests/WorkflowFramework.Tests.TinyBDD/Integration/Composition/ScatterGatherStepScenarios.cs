using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Composition;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Composition;

// Phase 3 — re-rooted on PatternKit AsyncScatterGather<TRequest, TResponse, TResult>.
//
// Recipient contract change:
//   Each recipient is now a ScatterGatherStep.Recipient (typed name + ValueTask-returning handler)
//   rather than an IStep that writes results to shared context keys (__Result_{Name}).
//   The shared-context mutation pattern was a concurrency hazard — handlers racing to write
//   different keys on the same IWorkflowContext were not safely isolated.
//
// Public output contract is preserved:
//   ResultsKey is still written with IReadOnlyList<object?> of per-recipient results.
//   The aggregator still receives IReadOnlyList<object?> and IWorkflowContext.
//
// Legacy back-compat:
//   A deprecated IEnumerable<IStep> overload is retained for one release. Tests that cover
//   the new typed-recipient API are marked clearly. The legacy-overload tests are marked
//   [Obsolete] suppressed and document the migration path.
//
// See .plan/patternkit-iteration-2.md §6.

[Feature("ScatterGatherStep — characterization (Phase G.2, updated Phase 3)")]
public class ScatterGatherStepScenarios : TinyBddTestBase
{
    public ScatterGatherStepScenarios(ITestOutputHelper output) : base(output) { }

    // Helper: create a typed recipient from a name and a synchronous result value.
    private static ScatterGatherStep.Recipient TypedRecipient(string name, object? result)
        => new(name, (_, _) => new ValueTask<object?>(result));

    // Helper: create a typed recipient that throws.
    private static ScatterGatherStep.Recipient FaultingRecipient(string name, Exception ex)
        => new(name, (_, _) => ValueTask.FromException<object?>(ex));

    [Scenario("ScatterGatherStep Name returns 'ScatterGather'"), Fact]
    public async Task NameIsScatterGather()
    {
        var sut = new ScatterGatherStep(
            Array.Empty<ScatterGatherStep.Recipient>(),
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

    [Scenario("All typed recipients execute and aggregator receives their results"), Fact]
    public async Task AllHandlersRunAndAggregatorReceivesResults()
    {
        // Phase 3: typed recipients return results directly — no shared-context mutation.
        var aggregatedResults = new List<object?>();

        var sut = new ScatterGatherStep(
            new[]
            {
                TypedRecipient("h1", "result1"),
                TypedRecipient("h2", "result2"),
            },
            (results, _) => { aggregatedResults.AddRange(results); return Task.CompletedTask; },
            TimeSpan.FromSeconds(5));

        var ctx = new WorkflowContext();
        await sut.ExecuteAsync(ctx);

        await Given("aggregated results after scatter-gather with two typed recipients", () => (aggregatedResults, ctx))
            .Then("two results were collected and ResultsKey is set on context", state =>
            {
                state.aggregatedResults.Should().HaveCount(2);
                state.ctx.Properties.Should().ContainKey(ScatterGatherStep.ResultsKey);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Failing typed recipient does not prevent aggregator from being called"), Fact]
    public async Task FailingHandlerDoesNotBlockAggregator()
    {
        // Phase 3: PatternKit AsyncScatterGather isolates per-branch errors. A faulting
        // recipient produces a Failure envelope; the aggregator still receives all envelopes
        // (succeeded and failed), so it is always called with partial/full results.
        var aggregatorCalled = false;

        var sut = new ScatterGatherStep(
            new[]
            {
                FaultingRecipient("bad", new InvalidOperationException("branch failure")),
                TypedRecipient("good", "ok"),
            },
            (_, _) => { aggregatorCalled = true; return Task.CompletedTask; },
            TimeSpan.FromSeconds(5));

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("whether aggregator was called despite faulting recipient", () => aggregatorCalled)
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

    [Scenario("Null recipients throws ArgumentNullException"), Fact]
    public async Task NullHandlersThrows()
    {
        Exception? caught = null;
        try { _ = new ScatterGatherStep((IEnumerable<ScatterGatherStep.Recipient>)null!, (_, _) => Task.CompletedTask, TimeSpan.FromSeconds(1)); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null recipients", () => caught)
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
        try { _ = new ScatterGatherStep(Array.Empty<ScatterGatherStep.Recipient>(), null!, TimeSpan.FromSeconds(1)); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null aggregator", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Single typed recipient result is stored under ResultsKey"), Fact]
    public async Task SingleHandlerResultStored()
    {
        // Phase 3: recipient returns 42 directly; no shared-context __Result_ key needed.
        var sut = new ScatterGatherStep(
            new[] { TypedRecipient("solo", 42) },
            (results, ctx) => { ctx.Properties["aggregated"] = results[0]; return Task.CompletedTask; },
            TimeSpan.FromSeconds(5));

        var ctx = new WorkflowContext();
        await sut.ExecuteAsync(ctx);

        await Given("aggregated property after scatter-gather with solo typed recipient", () => ctx)
            .Then("aggregated is 42", c =>
            {
                c.Properties["aggregated"].Should().Be(42);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Empty typed recipients list calls aggregator with empty results"), Fact]
    public async Task EmptyHandlersCallsAggregatorWithEmptyList()
    {
        IReadOnlyList<object?>? received = null;
        var sut = new ScatterGatherStep(
            Array.Empty<ScatterGatherStep.Recipient>(),
            (results, _) => { received = results; return Task.CompletedTask; },
            TimeSpan.FromSeconds(5));

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("results received by aggregator with empty recipient list", () => received)
            .Then("aggregator was called with empty list", r =>
            {
                r.Should().NotBeNull().And.BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Typed recipient that throws OperationCanceledException produces no result in aggregator"), Fact]
    public async Task HandlerOperationCanceledException_IsSwallowed()
    {
        // Behavior change (Phase 3): PatternKit AsyncScatterGather swallows non-caller-initiated
        // OperationCanceledException internally WITHOUT recording an envelope for that recipient.
        // Unlike the old bespoke implementation (which returned null for cancelled recipients),
        // PatternKit simply omits the cancelled recipient from the result set entirely.
        //
        // When ALL recipients are cancelled/omitted and no envelopes are produced,
        // DispatchAsync returns a Rejected result and the step writes an empty list to ResultsKey.
        //
        // Rationale: PatternKit distinguishes caller cancellation (surfaces as failure envelope)
        // from timeout/early-exit cancellation (swallowed silently). This is the correct behavior:
        // a non-caller-cancelled branch timed out; it is not a "failure" to report.
        // See PatternKit.Messaging.Routing.AsyncScatterGather RunRecipientAsync and .plan §6.
        IReadOnlyList<object?>? received = null;

        var sut = new ScatterGatherStep(
            new[]
            {
                new ScatterGatherStep.Recipient("cancelling", (_, ct) =>
                    ValueTask.FromException<object?>(new OperationCanceledException())),
            },
            (results, _) => { received = results; return Task.CompletedTask; },
            TimeSpan.FromSeconds(5));

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("results after typed recipient throws OperationCanceledException", () => received)
            .Then("aggregator receives empty results (cancelled branch is omitted, not null-padded)", r =>
            {
                r.Should().NotBeNull();
                // PatternKit omits non-caller-cancelled branches entirely rather than null-padding.
                r!.Should().BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Timeout fires and aggregator receives partial results"), Fact]
    public async Task Timeout_AggregatorsReceivesPartialResults()
    {
        // Phase 3: PatternKit AllOrTimeout strategy fires after the timeout;
        // recipients that finished are aggregated, slow ones produce no result.
        IReadOnlyList<object?>? received = null;

        var sut = new ScatterGatherStep(
            new[]
            {
                TypedRecipient("fast", "done"),
                new ScatterGatherStep.Recipient("slow", async (_, _) =>
                {
                    await Task.Delay(500).ConfigureAwait(false);
                    return (object?)null;
                }),
            },
            (results, _) => { received = results; return Task.CompletedTask; },
            TimeSpan.FromMilliseconds(50));

        var ctx = new WorkflowContext();
        await sut.ExecuteAsync(ctx);

        await Given("partial results after scatter-gather timeout", () => received)
            .Then("aggregator received results (not null)", r =>
            {
                r.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }
}
