using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Transformation;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Transformation;

// Bespoke kept: ContentFilterStep is structurally identical to ContentEnricherStep —
// a side-effect delegate with no typed I/O. PatternKit Decorator or Specification
// patterns require output values or boolean predicates, neither of which models a
// context-mutation filter cleanly. Characterization-only coverage locks current contract.

[Feature("ContentFilterStep — characterization (Phase G.5)")]
public class ContentFilterStepScenarios : TinyBddTestBase
{
    public ContentFilterStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("ContentFilterStep.Name defaults to 'ContentFilter'"), Fact]
    public async Task DefaultNameIsContentFilter()
    {
        var sut = new ContentFilterStep(_ => Task.CompletedTask);

        await Given("ContentFilterStep with no name override", () => sut)
            .Then("Name is 'ContentFilter'", s =>
            {
                s.Name.Should().Be("ContentFilter");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Custom name is reflected in the Name property"), Fact]
    public async Task CustomNameReflectedInProperty()
    {
        var sut = new ContentFilterStep(_ => Task.CompletedTask, "MyFilter");

        await Given("ContentFilterStep with custom name 'MyFilter'", () => sut)
            .Then("Name is 'MyFilter'", s =>
            {
                s.Name.Should().Be("MyFilter");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null filterAction throws ArgumentNullException"), Fact]
    public async Task NullFilterActionThrows()
    {
        Exception? caught = null;
        try { _ = new ContentFilterStep(null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null filterAction", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ExecuteAsync invokes the filterAction with the context"), Fact]
    public async Task ExecuteInvokesFilterAction()
    {
        IWorkflowContext? received = null;
        var ctx = new WorkflowContext();
        var sut = new ContentFilterStep(c => { received = c; return Task.CompletedTask; });
        await sut.ExecuteAsync(ctx);

        await Given("context received by filterAction", () => received)
            .Then("it is the same context passed to ExecuteAsync", r =>
            {
                r.Should().BeSameAs(ctx);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("FilterAction can remove properties from context"), Fact]
    public async Task FilterActionCanRemoveProperties()
    {
        var ctx = new WorkflowContext();
        ctx.Properties["remove-me"] = "value";
        ctx.Properties["keep-me"] = "kept";

        var sut = new ContentFilterStep(c =>
        {
            c.Properties.Remove("remove-me");
            return Task.CompletedTask;
        });
        await sut.ExecuteAsync(ctx);

        await Given("context after filter action", () => ctx)
            .Then("'remove-me' is gone and 'keep-me' remains", c =>
            {
                c.Properties.Should().NotContainKey("remove-me");
                c.Properties.Should().ContainKey("keep-me");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Exception from filterAction propagates to caller"), Fact]
    public async Task FilterActionExceptionPropagates()
    {
        var sut = new ContentFilterStep(_ => throw new InvalidOperationException("filter failed"));
        Exception? caught = null;
        try { await sut.ExecuteAsync(new WorkflowContext()); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("exception from filterAction", () => caught)
            .Then("InvalidOperationException propagates", ex =>
            {
                ex.Should().NotBeNull();
                ex!.Message.Should().Be("filter failed");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ExecuteAsync awaits the filterAction's task before returning"), Fact]
    public async Task ExecuteAwaitsFilterAction()
    {
        var order = new List<string>();
        var sut = new ContentFilterStep(async _ =>
        {
            await Task.Delay(10);
            order.Add("filter");
        });
        await sut.ExecuteAsync(new WorkflowContext());
        order.Add("after");

        await Given("execution order after awaited filter", () => order)
            .Then("filter completes before after", o =>
            {
                o.Should().ContainInOrder("filter", "after");
                return true;
            })
            .AssertPassed();
    }
}
