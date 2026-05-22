using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Routing;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Routing;

[Feature("ContentBasedRouterStep — characterization (Phase G.1)")]
public class ContentBasedRouterStepScenarios : TinyBddTestBase
{
    public ContentBasedRouterStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("First matching predicate selects its branch"), Fact]
    public async Task FirstMatchingPredicateSelectsBranch()
    {
        var branchAExecuted = false;
        var branchBExecuted = false;

        var branchA = Substitute.For<IStep>();
        branchA.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(_ => { branchAExecuted = true; return Task.CompletedTask; });

        var branchB = Substitute.For<IStep>();
        branchB.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(_ => { branchBExecuted = true; return Task.CompletedTask; });

        var ctx = new WorkflowContext();
        ctx.Properties["type"] = "A";

        var sut = new ContentBasedRouterStep(new[]
        {
            ((Func<IWorkflowContext, bool>)(c => c.Properties.TryGetValue("type", out var t) && (string?)t == "A"), branchA),
            ((Func<IWorkflowContext, bool>)(c => c.Properties.TryGetValue("type", out var t) && (string?)t == "B"), branchB)
        });

        await sut.ExecuteAsync(ctx);

        await Given("which branches executed for a type-A context", () => (branchAExecuted, branchBExecuted))
            .Then("only branch A ran", state =>
            {
                state.branchAExecuted.Should().BeTrue();
                state.branchBExecuted.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Only first matching predicate executes even if multiple match"), Fact]
    public async Task OnlyFirstMatchExecutes()
    {
        var callCount = 0;

        var step = Substitute.For<IStep>();
        step.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(_ => { callCount++; return Task.CompletedTask; });

        // Both predicates always true — only first should fire
        var sut = new ContentBasedRouterStep(new[]
        {
            ((Func<IWorkflowContext, bool>)(_ => true), step),
            ((Func<IWorkflowContext, bool>)(_ => true), step)
        });

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("call count when two always-true predicates exist", () => callCount)
            .Then("the branch step was called exactly once", count =>
            {
                count.Should().Be(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("No matching predicate invokes default route"), Fact]
    public async Task NoMatchUsesDefaultRoute()
    {
        var defaultExecuted = false;
        var noMatchStep = Substitute.For<IStep>();
        noMatchStep.ExecuteAsync(Arg.Any<IWorkflowContext>()).Returns(Task.CompletedTask);

        var defaultStep = Substitute.For<IStep>();
        defaultStep.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(_ => { defaultExecuted = true; return Task.CompletedTask; });

        var sut = new ContentBasedRouterStep(
            new[] { ((Func<IWorkflowContext, bool>)(_ => false), noMatchStep) },
            defaultRoute: defaultStep);

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("whether default route executed when no predicate matched", () => defaultExecuted)
            .Then("default route was executed", executed =>
            {
                executed.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("No match and no default route completes silently"), Fact]
    public async Task NoMatchNoDefaultCompletesWithoutError()
    {
        var noMatchStep = Substitute.For<IStep>();
        var sut = new ContentBasedRouterStep(
            new[] { ((Func<IWorkflowContext, bool>)(_ => false), noMatchStep) });

        Exception? caught = null;
        try { await sut.ExecuteAsync(new WorkflowContext()); }
        catch (Exception ex) { caught = ex; }

        await Given("exception when no predicate matches and no default", () => caught)
            .Then("no exception is thrown", ex =>
            {
                ex.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Empty routes list with default route executes default"), Fact]
    public async Task EmptyRoutesWithDefaultExecutesDefault()
    {
        var defaultExecuted = false;
        var defaultStep = Substitute.For<IStep>();
        defaultStep.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(_ => { defaultExecuted = true; return Task.CompletedTask; });

        var sut = new ContentBasedRouterStep(
            Array.Empty<(Func<IWorkflowContext, bool>, IStep)>(),
            defaultRoute: defaultStep);

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("whether default route ran with empty predicate list", () => defaultExecuted)
            .Then("default was executed", executed =>
            {
                executed.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ContentBasedRouterStep Name returns 'ContentBasedRouter'"), Fact]
    public async Task NameIsContentBasedRouter()
    {
        var sut = new ContentBasedRouterStep(
            Array.Empty<(Func<IWorkflowContext, bool>, IStep)>());

        await Given("ContentBasedRouterStep instance", () => sut)
            .Then("Name is 'ContentBasedRouter'", s =>
            {
                s.Name.Should().Be("ContentBasedRouter");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null routes throws ArgumentNullException"), Fact]
    public async Task NullRoutesThrows()
    {
        Exception? caught = null;
        try { _ = new ContentBasedRouterStep(null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null routes", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Predicate receiving context can read Properties"), Fact]
    public async Task PredicateCanReadContextProperties()
    {
        var matchedKey = string.Empty;
        var matchedStep = Substitute.For<IStep>();
        matchedStep.ExecuteAsync(Arg.Any<IWorkflowContext>()).Returns(Task.CompletedTask);

        var ctx = new WorkflowContext();
        ctx.Properties["key"] = "value";

        var sut = new ContentBasedRouterStep(new[]
        {
            ((Func<IWorkflowContext, bool>)(c =>
            {
                matchedKey = c.Properties.TryGetValue("key", out var v) ? v?.ToString() ?? "" : "";
                return matchedKey == "value";
            }), matchedStep)
        });

        await sut.ExecuteAsync(ctx);

        await Given("the key read by the predicate from context", () => matchedKey)
            .Then("predicate correctly accessed 'value'", k =>
            {
                k.Should().Be("value");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Second route executes when first predicate is false"), Fact]
    public async Task SecondRouteRunsWhenFirstDoesNotMatch()
    {
        var secondExecuted = false;
        var firstStep = Substitute.For<IStep>();
        var secondStep = Substitute.For<IStep>();
        secondStep.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(_ => { secondExecuted = true; return Task.CompletedTask; });

        var sut = new ContentBasedRouterStep(new[]
        {
            ((Func<IWorkflowContext, bool>)(_ => false), firstStep),
            ((Func<IWorkflowContext, bool>)(_ => true), secondStep)
        });

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("whether second route ran when first predicate returned false", () => secondExecuted)
            .Then("second route was executed", executed =>
            {
                executed.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }
}
