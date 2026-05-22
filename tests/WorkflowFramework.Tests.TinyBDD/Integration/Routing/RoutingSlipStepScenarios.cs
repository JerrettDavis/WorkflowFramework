using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Routing;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Routing;

[Feature("RoutingSlipStep — characterization (Phase G.1)")]
public class RoutingSlipStepScenarios : TinyBddTestBase
{
    public RoutingSlipStepScenarios(ITestOutputHelper output) : base(output) { }

    // ─── RoutingSlip model ───────────────────────────────────────────────────

    [Scenario("RoutingSlip starts at index 0"), Fact]
    public async Task RoutingSlipStartsAtZero()
    {
        var slip = new RoutingSlip(new[] { "a", "b" });

        await Given("a freshly created routing slip", () => slip)
            .Then("current index is 0", s =>
            {
                s.CurrentIndex.Should().Be(0);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("RoutingSlip CurrentStep returns first item before any advance"), Fact]
    public async Task RoutingSlipCurrentStepIsFirstBeforeAdvance()
    {
        var slip = new RoutingSlip(new[] { "step-a", "step-b" });

        await Given("a routing slip with two entries", () => slip)
            .Then("current step is the first itinerary entry", s =>
            {
                s.CurrentStep.Should().Be("step-a");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("RoutingSlip Advance moves to next step"), Fact]
    public async Task RoutingSlipAdvanceMovesToNext()
    {
        var slip = new RoutingSlip(new[] { "step-a", "step-b" });
        slip.Advance();

        await Given("a routing slip after one Advance call", () => slip)
            .Then("current step is the second itinerary entry", s =>
            {
                s.CurrentStep.Should().Be("step-b");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("RoutingSlip Advance returns false when past last entry"), Fact]
    public async Task RoutingSlipAdvancePastLastReturnsFalse()
    {
        var slip = new RoutingSlip(new[] { "only" });
        slip.Advance(); // now past end

        await Given("a routing slip advanced past the last entry", () => slip)
            .Then("CurrentStep is null and Advance returns false", s =>
            {
                s.CurrentStep.Should().BeNull();
                s.Advance().Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("RoutingSlip Itinerary is read-only"), Fact]
    public async Task RoutingSlipItineraryIsReadOnly()
    {
        var slip = new RoutingSlip(new[] { "x" });

        await Given("a routing slip", () => slip)
            .Then("Itinerary is IReadOnlyList", s =>
            {
                s.Itinerary.Should().NotBeNull();
                s.Itinerary.Should().HaveCount(1);
                s.Itinerary[0].Should().Be("x");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("RoutingSlip null itinerary throws ArgumentNullException"), Fact]
    public async Task RoutingSlipNullItineraryThrows()
    {
        Exception? caught = null;
        try { _ = new RoutingSlip(null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("creating RoutingSlip with null itinerary", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    // ─── RoutingSlipStep ─────────────────────────────────────────────────────

    [Scenario("All destinations visited in order"), Fact]
    public async Task AllDestinationsVisitedInOrder()
    {
        var visited = new List<string>();
        var step1 = Substitute.For<IStep>();
        step1.Name.Returns("s1");
        step1.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci => { visited.Add("s1"); return Task.CompletedTask; });

        var step2 = Substitute.For<IStep>();
        step2.Name.Returns("s2");
        step2.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci => { visited.Add("s2"); return Task.CompletedTask; });

        var slip = new RoutingSlip(new[] { "s1", "s2" });
        var registry = new Dictionary<string, IStep> { ["s1"] = step1, ["s2"] = step2 };
        var sut = new RoutingSlipStep(_ => slip, registry);

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("visit order after executing routing slip with two steps", () => visited)
            .Then("s1 was visited before s2", order =>
            {
                order.Should().ContainInOrder("s1", "s2");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Routing slip stored on context under RoutingSlipKey"), Fact]
    public async Task SlipStoredOnContext()
    {
        var step = Substitute.For<IStep>();
        step.Name.Returns("only");
        step.ExecuteAsync(Arg.Any<IWorkflowContext>()).Returns(Task.CompletedTask);

        var slip = new RoutingSlip(new[] { "only" });
        var registry = new Dictionary<string, IStep> { ["only"] = step };
        var sut = new RoutingSlipStep(_ => slip, registry);
        var ctx = new WorkflowContext();

        await sut.ExecuteAsync(ctx);

        await Given("context after routing slip execution", () => ctx)
            .Then("the routing slip is stored under RoutingSlipKey", c =>
            {
                c.Properties.Should().ContainKey(RoutingSlipStep.RoutingSlipKey);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Missing step in registry throws InvalidOperationException"), Fact]
    public async Task MissingStepThrows()
    {
        var slip = new RoutingSlip(new[] { "ghost" });
        var registry = new Dictionary<string, IStep>();
        var sut = new RoutingSlipStep(_ => slip, registry);

        Exception? caught = null;
        try { await sut.ExecuteAsync(new WorkflowContext()); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("exception when routing slip references unregistered step", () => caught)
            .Then("InvalidOperationException mentions the missing step name", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<InvalidOperationException>();
                ex!.Message.Should().Contain("ghost");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Empty itinerary completes without executing any step"), Fact]
    public async Task EmptyItineraryCompletesImmediately()
    {
        var slip = new RoutingSlip(Enumerable.Empty<string>());
        var sut = new RoutingSlipStep(_ => slip, new Dictionary<string, IStep>());

        Exception? caught = null;
        try { await sut.ExecuteAsync(new WorkflowContext()); }
        catch (Exception ex) { caught = ex; }

        await Given("exception after executing routing slip with empty itinerary", () => caught)
            .Then("no exception is thrown", ex =>
            {
                ex.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Routing stops when context is aborted mid-slip"), Fact]
    public async Task RoutingStopsOnAbort()
    {
        var visitCount = 0;

        var step1 = Substitute.For<IStep>();
        step1.Name.Returns("a");
        step1.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci =>
            {
                visitCount++;
                ((IWorkflowContext)ci[0]).IsAborted = true;
                return Task.CompletedTask;
            });

        var step2 = Substitute.For<IStep>();
        step2.Name.Returns("b");
        step2.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci => { visitCount++; return Task.CompletedTask; });

        var slip = new RoutingSlip(new[] { "a", "b" });
        var registry = new Dictionary<string, IStep> { ["a"] = step1, ["b"] = step2 };
        var sut = new RoutingSlipStep(_ => slip, registry);

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("visit count after abort is set mid-slip", () => visitCount)
            .Then("only the first step ran before the abort", count =>
            {
                count.Should().Be(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("RoutingSlipStep Name returns 'RoutingSlip'"), Fact]
    public async Task NameIsRoutingSlip()
    {
        var slip = new RoutingSlip(new[] { "x" });
        var registry = new Dictionary<string, IStep>
        {
            ["x"] = Substitute.For<IStep>()
        };
        var sut = new RoutingSlipStep(_ => slip, registry);

        await Given("RoutingSlipStep instance", () => sut)
            .Then("Name property returns 'RoutingSlip'", s =>
            {
                s.Name.Should().Be("RoutingSlip");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null slipSelector throws ArgumentNullException"), Fact]
    public async Task NullSlipSelectorThrows()
    {
        Exception? caught = null;
        try { _ = new RoutingSlipStep(null!, new Dictionary<string, IStep>()); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null slip selector", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null stepRegistry throws ArgumentNullException"), Fact]
    public async Task NullStepRegistryThrows()
    {
        Exception? caught = null;
        try { _ = new RoutingSlipStep(_ => new RoutingSlip(new[] { "x" }), null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null step registry", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Single-step itinerary executes that one step"), Fact]
    public async Task SingleStepItineraryExecutesOnce()
    {
        var callCount = 0;
        var step = Substitute.For<IStep>();
        step.Name.Returns("solo");
        step.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(_ => { callCount++; return Task.CompletedTask; });

        var slip = new RoutingSlip(new[] { "solo" });
        var registry = new Dictionary<string, IStep> { ["solo"] = step };
        var sut = new RoutingSlipStep(_ => slip, registry);

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("call count after single-entry itinerary", () => callCount)
            .Then("the step was called exactly once", count =>
            {
                count.Should().Be(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("RoutingSlipKey constant value is expected string"), Fact]
    public async Task RoutingSlipKeyHasExpectedValue()
    {
        await Given("RoutingSlipStep.RoutingSlipKey constant", () => RoutingSlipStep.RoutingSlipKey)
            .Then("it equals '__RoutingSlip'", key =>
            {
                key.Should().Be("__RoutingSlip");
                return true;
            })
            .AssertPassed();
    }
}
