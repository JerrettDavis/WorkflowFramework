using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Routing;

namespace WorkflowFramework.Tests.TinyBDD.Integration;

[Feature("Content based router step")]
public class ContentBasedRouterStepTests : TinyBddTestBase
{
    public ContentBasedRouterStepTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Matching predicate routes to the correct branch"), Fact]
    public async Task MatchingPredicateSelectsBranch()
    {
        var branchAExecuted = false;
        var branchBExecuted = false;

        var branchA = Substitute.For<IStep>();
        branchA.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(_ => { branchAExecuted = true; return Task.CompletedTask; });

        var branchB = Substitute.For<IStep>();
        branchB.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(_ => { branchBExecuted = true; return Task.CompletedTask; });

        var router = new ContentBasedRouterStep(new[]
        {
            ((Func<IWorkflowContext, bool>)(ctx => ctx.Properties.TryGetValue("type", out var t) && (string?)t == "A"), branchA),
            ((Func<IWorkflowContext, bool>)(ctx => ctx.Properties.TryGetValue("type", out var t) && (string?)t == "B"), branchB)
        });

        var context = new WorkflowContext();
        context.Properties["type"] = "A";
        await router.ExecuteAsync(context);

        await Given("execution results after routing a type-A context", () => (branchAExecuted, branchBExecuted))
            .Then("only branch A was executed", state =>
            {
                state.branchAExecuted.Should().BeTrue();
                state.branchBExecuted.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("No matching predicate invokes the default route"), Fact]
    public async Task NoMatchUsesDefaultRoute()
    {
        var defaultExecuted = false;

        var noMatch = Substitute.For<IStep>();
        noMatch.ExecuteAsync(Arg.Any<IWorkflowContext>()).Returns(Task.CompletedTask);

        var defaultStep = Substitute.For<IStep>();
        defaultStep.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(_ => { defaultExecuted = true; return Task.CompletedTask; });

        var router = new ContentBasedRouterStep(
            new[] { ((Func<IWorkflowContext, bool>)(_ => false), noMatch) },
            defaultRoute: defaultStep);

        await router.ExecuteAsync(new WorkflowContext());

        await Given("whether the default route executed when no predicate matched", () => defaultExecuted)
            .Then("the default route was executed", executed =>
            {
                executed.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("No matching predicate and no default route completes without error"), Fact]
    public async Task NoMatchNoDefaultCompletes()
    {
        var noMatch = Substitute.For<IStep>();
        var router = new ContentBasedRouterStep(
            new[] { ((Func<IWorkflowContext, bool>)(_ => false), noMatch) });

        Exception? caught = null;
        try { await router.ExecuteAsync(new WorkflowContext()); }
        catch (Exception ex) { caught = ex; }

        await Given("the exception (if any) after routing with no match and no default", () => caught)
            .Then("no exception was thrown", ex =>
            {
                ex.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }
}
