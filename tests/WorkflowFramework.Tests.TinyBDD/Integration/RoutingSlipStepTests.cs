using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Routing;

namespace WorkflowFramework.Tests.TinyBDD.Integration;

[Feature("Routing slip step")]
public class RoutingSlipStepTests : TinyBddTestBase
{
    public RoutingSlipStepTests(ITestOutputHelper output) : base(output) { }

    [Scenario("All destinations in the slip are visited in order"), Fact]
    public async Task DestinationsVisitedInOrder()
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
        var routingSlipStep = new RoutingSlipStep(_ => slip, registry);
        await routingSlipStep.ExecuteAsync(new WorkflowContext());

        await Given("the visit order list after routing slip execution", () => visited)
            .Then("s1 was visited before s2", order =>
            {
                order.Should().ContainInOrder("s1", "s2");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Missing step name in registry throws InvalidOperationException"), Fact]
    public async Task MissingStepThrows()
    {
        var slip = new RoutingSlip(new[] { "missing-step" });
        var registry = new Dictionary<string, IStep>();
        var routingSlipStep = new RoutingSlipStep(_ => slip, registry);

        Exception? caught = null;
        try { await routingSlipStep.ExecuteAsync(new WorkflowContext()); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("exception caught when routing slip references an unregistered step", () => caught)
            .Then("an InvalidOperationException was thrown mentioning the missing step", ex =>
            {
                ex.Should().NotBeNull();
                ex!.Message.Should().Contain("missing-step");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Routing slip is stored on the context properties"), Fact]
    public async Task SlipIsStoredOnContext()
    {
        var step = Substitute.For<IStep>();
        step.Name.Returns("only");
        step.ExecuteAsync(Arg.Any<IWorkflowContext>()).Returns(Task.CompletedTask);

        var slip = new RoutingSlip(new[] { "only" });
        var registry = new Dictionary<string, IStep> { ["only"] = step };
        var routingSlipStep = new RoutingSlipStep(_ => slip, registry);
        var context = new WorkflowContext();
        await routingSlipStep.ExecuteAsync(context);

        await Given("context after routing slip step completes", () => context)
            .Then("the routing slip is stored under the RoutingSlipKey property", ctx =>
            {
                ctx.Properties.Should().ContainKey(RoutingSlipStep.RoutingSlipKey);
                return true;
            })
            .AssertPassed();
    }
}
