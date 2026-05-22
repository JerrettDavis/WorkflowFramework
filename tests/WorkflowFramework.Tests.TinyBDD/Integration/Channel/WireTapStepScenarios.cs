using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Channel;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Channel;

[Feature("WireTapStep — characterization (Phase G.3)")]
public class WireTapStepScenarios : TinyBddTestBase
{
    public WireTapStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("WireTapStep Name returns 'WireTap'"), Fact]
    public async Task NameIsWireTap()
    {
        var sut = new WireTapStep(_ => Task.CompletedTask);

        await Given("WireTapStep instance", () => sut)
            .Then("Name is 'WireTap'", s =>
            {
                s.Name.Should().Be("WireTap");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Tap action runs on happy path"), Fact]
    public async Task TapActionRunsOnHappyPath()
    {
        var tapCalled = false;
        var sut = new WireTapStep(_ => { tapCalled = true; return Task.CompletedTask; });

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("whether tap action was called", () => tapCalled)
            .Then("tap action ran", called =>
            {
                called.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Tap action receives workflow context"), Fact]
    public async Task TapActionReceivesContext()
    {
        IWorkflowContext? received = null;
        var ctx = new WorkflowContext();
        ctx.Properties["token"] = "abc";

        var sut = new WireTapStep(c => { received = c; return Task.CompletedTask; });
        await sut.ExecuteAsync(ctx);

        await Given("context received by tap action", () => received)
            .Then("it has the token property", rc =>
            {
                rc.Should().NotBeNull();
                rc!.Properties["token"].Should().Be("abc");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Tap action error is swallowed when swallowErrors is true (default)"), Fact]
    public async Task TapErrorIsSwallowedByDefault()
    {
        var sut = new WireTapStep(_ => throw new Exception("tap error"), swallowErrors: true);

        Exception? caught = null;
        try { await sut.ExecuteAsync(new WorkflowContext()); }
        catch (Exception ex) { caught = ex; }

        await Given("exception after faulting tap with swallowErrors=true", () => caught)
            .Then("no exception propagates", ex =>
            {
                ex.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Tap action error propagates when swallowErrors is false"), Fact]
    public async Task TapErrorPropagatesWhenSwallowFalse()
    {
        var sut = new WireTapStep(_ => throw new InvalidOperationException("tap exploded"), swallowErrors: false);

        Exception? caught = null;
        try { await sut.ExecuteAsync(new WorkflowContext()); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("exception after faulting tap with swallowErrors=false", () => caught)
            .Then("InvalidOperationException propagates", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<InvalidOperationException>();
                ex!.Message.Should().Contain("tap exploded");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null tap action throws ArgumentNullException"), Fact]
    public async Task NullTapActionThrows()
    {
        Exception? caught = null;
        try { _ = new WireTapStep(null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null tap action", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WireTap does not mutate context IsAborted"), Fact]
    public async Task WireTapDoesNotAbortContext()
    {
        var ctx = new WorkflowContext();
        var sut = new WireTapStep(_ => Task.CompletedTask);

        await sut.ExecuteAsync(ctx);

        await Given("context IsAborted after wire tap", () => ctx.IsAborted)
            .Then("context is not aborted", aborted =>
            {
                aborted.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Async tap action is awaited"), Fact]
    public async Task AsyncTapActionIsAwaited()
    {
        var tapCompleted = false;
        var sut = new WireTapStep(async _ =>
        {
            await Task.Delay(10);
            tapCompleted = true;
        });

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("tapCompleted flag after async tap action", () => tapCompleted)
            .Then("async tap completed before ExecuteAsync returned", completed =>
            {
                completed.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }
}
