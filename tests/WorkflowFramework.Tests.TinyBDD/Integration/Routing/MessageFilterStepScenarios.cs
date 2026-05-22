using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Routing;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Routing;

[Feature("MessageFilterStep — characterization (Phase G.1)")]
public class MessageFilterStepScenarios : TinyBddTestBase
{
    public MessageFilterStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("MessageFilterStep Name returns 'MessageFilter'"), Fact]
    public async Task NameIsMessageFilter()
    {
        var sut = new MessageFilterStep(_ => true);

        await Given("MessageFilterStep instance", () => sut)
            .Then("Name is 'MessageFilter'", s =>
            {
                s.Name.Should().Be("MessageFilter");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Matching predicate allows message through without aborting"), Fact]
    public async Task MatchingPredicateAllowsThrough()
    {
        var ctx = new WorkflowContext();
        var sut = new MessageFilterStep(_ => true);

        await sut.ExecuteAsync(ctx);

        await Given("context after filter with always-true predicate", () => ctx)
            .Then("context is not aborted", c =>
            {
                c.IsAborted.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Non-matching predicate aborts the workflow context"), Fact]
    public async Task NonMatchingPredicateAbortsContext()
    {
        var ctx = new WorkflowContext();
        var sut = new MessageFilterStep(_ => false);

        await sut.ExecuteAsync(ctx);

        await Given("context after filter with always-false predicate", () => ctx)
            .Then("context IsAborted is true", c =>
            {
                c.IsAborted.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null predicate throws ArgumentNullException"), Fact]
    public async Task NullPredicateThrows()
    {
        Exception? caught = null;
        try { _ = new MessageFilterStep(null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null predicate", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Predicate receives workflow context"), Fact]
    public async Task PredicateReceivesContext()
    {
        IWorkflowContext? received = null;
        var ctx = new WorkflowContext();
        ctx.Properties["sentinel"] = 42;

        var sut = new MessageFilterStep(c =>
        {
            received = c;
            return true;
        });

        await sut.ExecuteAsync(ctx);

        await Given("context received by predicate", () => received)
            .Then("it has the sentinel property", rc =>
            {
                rc.Should().NotBeNull();
                rc!.Properties["sentinel"].Should().Be(42);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Filter completes synchronously (returns completed Task)"), Fact]
    public async Task FilterCompletesWithCompletedTask()
    {
        var sut = new MessageFilterStep(_ => true);
        var task = sut.ExecuteAsync(new WorkflowContext());

        await Given("task returned by filter", () => task)
            .Then("task is in RanToCompletion status", t =>
            {
                t.IsCompleted.Should().BeTrue();
                t.IsFaulted.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Context with pre-existing abort remains aborted when predicate false"), Fact]
    public async Task AlreadyAbortedContextRemainsAborted()
    {
        var ctx = new WorkflowContext();
        ctx.IsAborted = true;
        var sut = new MessageFilterStep(_ => false);

        await sut.ExecuteAsync(ctx);

        await Given("context that was already aborted and filter predicate returned false", () => ctx)
            .Then("context IsAborted remains true", c =>
            {
                c.IsAborted.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }
}
