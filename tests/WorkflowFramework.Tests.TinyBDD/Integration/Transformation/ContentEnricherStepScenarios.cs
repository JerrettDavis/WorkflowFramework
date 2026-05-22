using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Transformation;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Transformation;

// Bespoke kept: ContentEnricherStep is a thin delegate-wrapper with no structured
// input→output type conversion. PatternKit AsyncDecorator<TIn,TOut> requires typed
// I/O; ContentEnricherStep operates on IWorkflowContext side-effects only. A forced
// swap would add indirection with no benefit. Characterization-only coverage is
// provided here to lock in the current contract.

[Feature("ContentEnricherStep — characterization (Phase G.5)")]
public class ContentEnricherStepScenarios : TinyBddTestBase
{
    public ContentEnricherStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("ContentEnricherStep.Name defaults to 'ContentEnricher'"), Fact]
    public async Task DefaultNameIsContentEnricher()
    {
        var sut = new ContentEnricherStep(_ => Task.CompletedTask);

        await Given("ContentEnricherStep with no name override", () => sut)
            .Then("Name is 'ContentEnricher'", s =>
            {
                s.Name.Should().Be("ContentEnricher");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Custom name is reflected in the Name property"), Fact]
    public async Task CustomNameReflectedInProperty()
    {
        var sut = new ContentEnricherStep(_ => Task.CompletedTask, "MyEnricher");

        await Given("ContentEnricherStep with custom name 'MyEnricher'", () => sut)
            .Then("Name is 'MyEnricher'", s =>
            {
                s.Name.Should().Be("MyEnricher");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null enrichAction throws ArgumentNullException"), Fact]
    public async Task NullEnrichActionThrows()
    {
        Exception? caught = null;
        try { _ = new ContentEnricherStep(null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null enrichAction", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ExecuteAsync invokes the enrichAction with the context"), Fact]
    public async Task ExecuteInvokesEnrichAction()
    {
        IWorkflowContext? received = null;
        var ctx = new WorkflowContext();
        var sut = new ContentEnricherStep(c => { received = c; return Task.CompletedTask; });
        await sut.ExecuteAsync(ctx);

        await Given("context received by enrichAction", () => received)
            .Then("it is the same context passed to ExecuteAsync", r =>
            {
                r.Should().BeSameAs(ctx);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("EnrichAction can mutate context properties"), Fact]
    public async Task EnrichActionCanMutateProperties()
    {
        var ctx = new WorkflowContext();
        var sut = new ContentEnricherStep(c =>
        {
            c.Properties["enriched"] = true;
            return Task.CompletedTask;
        });
        await sut.ExecuteAsync(ctx);

        await Given("context after enrichment", () => ctx)
            .Then("enriched property is set to true", c =>
            {
                c.Properties["enriched"].Should().Be(true);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Exception from enrichAction propagates to caller"), Fact]
    public async Task EnrichActionExceptionPropagates()
    {
        var sut = new ContentEnricherStep(_ => throw new InvalidOperationException("enrich failed"));
        Exception? caught = null;
        try { await sut.ExecuteAsync(new WorkflowContext()); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("exception from enrichAction", () => caught)
            .Then("InvalidOperationException propagates", ex =>
            {
                ex.Should().NotBeNull();
                ex!.Message.Should().Be("enrich failed");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ExecuteAsync awaits the enrichAction's task before returning"), Fact]
    public async Task ExecuteAwaitsEnrichAction()
    {
        var order = new List<string>();
        var sut = new ContentEnricherStep(async _ =>
        {
            await Task.Delay(10);
            order.Add("enrich");
        });
        await sut.ExecuteAsync(new WorkflowContext());
        order.Add("after");

        await Given("execution order after awaited enrich", () => order)
            .Then("enrich completes before after", o =>
            {
                o.Should().ContainInOrder("enrich", "after");
                return true;
            })
            .AssertPassed();
    }
}
