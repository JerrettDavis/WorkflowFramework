using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Routing;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Routing;

[Feature("RecipientListStep — characterization (Phase G.1)")]
public class RecipientListStepScenarios : TinyBddTestBase
{
    public RecipientListStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("RecipientListStep Name returns 'RecipientList'"), Fact]
    public async Task NameIsRecipientList()
    {
        var sut = new RecipientListStep(_ => Enumerable.Empty<IStep>());

        await Given("RecipientListStep instance", () => sut)
            .Then("Name is 'RecipientList'", s =>
            {
                s.Name.Should().Be("RecipientList");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Sequential recipients are all called in list order"), Fact]
    public async Task SequentialRecipientsCalledInOrder()
    {
        var visited = new List<string>();
        var r1 = Substitute.For<IStep>();
        r1.ExecuteAsync(Arg.Any<IWorkflowContext>()).Returns(_ => { visited.Add("r1"); return Task.CompletedTask; });
        var r2 = Substitute.For<IStep>();
        r2.ExecuteAsync(Arg.Any<IWorkflowContext>()).Returns(_ => { visited.Add("r2"); return Task.CompletedTask; });

        var sut = new RecipientListStep(_ => new[] { r1, r2 }, parallel: false);
        await sut.ExecuteAsync(new WorkflowContext());

        await Given("visit order from sequential recipient list", () => visited)
            .Then("r1 was visited before r2", order =>
            {
                order.Should().ContainInOrder("r1", "r2");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Parallel recipients all execute"), Fact]
    public async Task ParallelRecipientsAllExecute()
    {
        var count = 0;
        var r1 = Substitute.For<IStep>();
        r1.ExecuteAsync(Arg.Any<IWorkflowContext>()).Returns(_ => { Interlocked.Increment(ref count); return Task.CompletedTask; });
        var r2 = Substitute.For<IStep>();
        r2.ExecuteAsync(Arg.Any<IWorkflowContext>()).Returns(_ => { Interlocked.Increment(ref count); return Task.CompletedTask; });

        var sut = new RecipientListStep(_ => new[] { r1, r2 }, parallel: true);
        await sut.ExecuteAsync(new WorkflowContext());

        await Given("execution count after parallel recipient list", () => count)
            .Then("both recipients ran", c => { c.Should().Be(2); return true; })
            .AssertPassed();
    }

    [Scenario("Empty recipient list completes without error"), Fact]
    public async Task EmptyListCompletesWithoutError()
    {
        var sut = new RecipientListStep(_ => Enumerable.Empty<IStep>());

        Exception? caught = null;
        try { await sut.ExecuteAsync(new WorkflowContext()); }
        catch (Exception ex) { caught = ex; }

        await Given("exception when recipient list is empty", () => caught)
            .Then("no exception is thrown", ex =>
            {
                ex.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Sequential list stops when context is aborted"), Fact]
    public async Task SequentialListStopsOnAbort()
    {
        var r1 = Substitute.For<IStep>();
        r1.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci => { ((IWorkflowContext)ci[0]).IsAborted = true; return Task.CompletedTask; });

        var r2 = Substitute.For<IStep>();
        r2.ExecuteAsync(Arg.Any<IWorkflowContext>()).Returns(Task.CompletedTask);

        var sut = new RecipientListStep(_ => new[] { r1, r2 }, parallel: false);
        await sut.ExecuteAsync(new WorkflowContext());

        await r2.DidNotReceive().ExecuteAsync(Arg.Any<IWorkflowContext>());

        await Given("whether r2 was called after r1 aborted the context", () => true)
            .Then("r2 was NOT called due to abort", _ =>
            {
                // NSubstitute DidNotReceive above validates this; just need AssertPassed
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null recipient selector throws ArgumentNullException"), Fact]
    public async Task NullSelectorThrows()
    {
        Exception? caught = null;
        try { _ = new RecipientListStep(null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null recipient selector", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Recipient selector receives the workflow context"), Fact]
    public async Task SelectorReceivesContext()
    {
        IWorkflowContext? received = null;
        var ctx = new WorkflowContext();
        ctx.Properties["id"] = 99;

        var sut = new RecipientListStep(c =>
        {
            received = c;
            return Enumerable.Empty<IStep>();
        });

        await sut.ExecuteAsync(ctx);

        await Given("context received by selector", () => received)
            .Then("it has the id property", rc =>
            {
                rc.Should().NotBeNull();
                rc!.Properties["id"].Should().Be(99);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Single recipient in list is executed"), Fact]
    public async Task SingleRecipientExecuted()
    {
        var called = false;
        var step = Substitute.For<IStep>();
        step.ExecuteAsync(Arg.Any<IWorkflowContext>()).Returns(_ => { called = true; return Task.CompletedTask; });

        var sut = new RecipientListStep(_ => new[] { step });
        await sut.ExecuteAsync(new WorkflowContext());

        await Given("whether single recipient was called", () => called)
            .Then("the single recipient executed", c => { c.Should().BeTrue(); return true; })
            .AssertPassed();
    }
}
