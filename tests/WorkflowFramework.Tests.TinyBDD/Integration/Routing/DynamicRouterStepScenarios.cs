using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Routing;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Routing;

[Feature("DynamicRouterStep — characterization (Phase G.1)")]
public class DynamicRouterStepScenarios : TinyBddTestBase
{
    public DynamicRouterStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("DynamicRouterStep Name returns 'DynamicRouter'"), Fact]
    public async Task NameIsDynamicRouter()
    {
        var sut = new DynamicRouterStep(_ => null);

        await Given("DynamicRouterStep instance", () => sut)
            .Then("Name is 'DynamicRouter'", s =>
            {
                s.Name.Should().Be("DynamicRouter");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Routing function returning null completes immediately"), Fact]
    public async Task NullFromRoutingFunctionCompletesImmediately()
    {
        var sut = new DynamicRouterStep(_ => null);

        Exception? caught = null;
        try { await sut.ExecuteAsync(new WorkflowContext()); }
        catch (Exception ex) { caught = ex; }

        await Given("exception when routing function immediately returns null", () => caught)
            .Then("no exception is thrown", ex =>
            {
                ex.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Routing function executes returned step"), Fact]
    public async Task RoutingFunctionExecutesStep()
    {
        var executed = false;
        var step = Substitute.For<IStep>();
        step.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(_ => { executed = true; return Task.CompletedTask; });

        var callCount = 0;
        var sut = new DynamicRouterStep(_ =>
        {
            // Return step on first call, null on second
            return callCount++ == 0 ? step : null;
        });

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("whether the step returned by routing function was executed", () => executed)
            .Then("the step ran", ex => { ex.Should().BeTrue(); return true; })
            .AssertPassed();
    }

    [Scenario("Routing function called multiple times until null returned"), Fact]
    public async Task RoutingFunctionCalledUntilNull()
    {
        var steps = new List<IStep>();
        for (var i = 0; i < 3; i++)
        {
            var s = Substitute.For<IStep>();
            s.Name.Returns($"step{i}");
            s.ExecuteAsync(Arg.Any<IWorkflowContext>()).Returns(Task.CompletedTask);
            steps.Add(s);
        }

        var idx = 0;
        var sut = new DynamicRouterStep(_ => idx < steps.Count ? steps[idx++] : null);

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("number of steps chosen by dynamic router", () => idx)
            .Then("all three steps were invoked", count =>
            {
                count.Should().Be(3);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Routing stops when context is aborted"), Fact]
    public async Task RoutingStopsOnAbort()
    {
        var callCount = 0;
        var abortingStep = Substitute.For<IStep>();
        abortingStep.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci =>
            {
                callCount++;
                ((IWorkflowContext)ci[0]).IsAborted = true;
                return Task.CompletedTask;
            });

        var idx = 0;
        var sut = new DynamicRouterStep(_ =>
        {
            return idx++ < 10 ? abortingStep : null;
        });

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("call count when routing function aborts context on first call", () => callCount)
            .Then("only one step ran", count =>
            {
                count.Should().Be(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null routing function throws ArgumentNullException"), Fact]
    public async Task NullRoutingFunctionThrows()
    {
        Exception? caught = null;
        try { _ = new DynamicRouterStep(null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null routing function", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Routing function receives the workflow context"), Fact]
    public async Task RoutingFunctionReceivesContext()
    {
        IWorkflowContext? received = null;
        var ctx = new WorkflowContext();
        ctx.Properties["marker"] = "hello";

        var sut = new DynamicRouterStep(c =>
        {
            received = c;
            return null;
        });

        await sut.ExecuteAsync(ctx);

        await Given("context received by routing function", () => received)
            .Then("it is the same context passed to ExecuteAsync", rc =>
            {
                rc.Should().NotBeNull();
                rc!.Properties.Should().ContainKey("marker");
                return true;
            })
            .AssertPassed();
    }
}
