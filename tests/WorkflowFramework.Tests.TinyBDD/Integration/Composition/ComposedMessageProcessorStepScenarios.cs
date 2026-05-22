using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Composition;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Composition;

[Feature("ComposedMessageProcessorStep — characterization (Phase G.2)")]
public class ComposedMessageProcessorStepScenarios : TinyBddTestBase
{
    public ComposedMessageProcessorStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("ComposedMessageProcessorStep Name returns 'ComposedMessageProcessor'"), Fact]
    public async Task NameIsComposedMessageProcessor()
    {
        var processor = Substitute.For<IStep>();
        var sut = new ComposedMessageProcessorStep(
            _ => Enumerable.Empty<object>(),
            processor,
            (_, _) => Task.CompletedTask);

        await Given("ComposedMessageProcessorStep instance", () => sut)
            .Then("Name is 'ComposedMessageProcessor'", s =>
            {
                s.Name.Should().Be("ComposedMessageProcessor");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Processor runs for each split item and aggregator receives results"), Fact]
    public async Task ProcessorRunsForEachItemAndAggregatorReceivesResults()
    {
        var processedItems = new List<object?>();
        IReadOnlyList<object>? aggregatorResults = null;

        var processor = Substitute.For<IStep>();
        processor.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci =>
            {
                var ctx = (IWorkflowContext)ci[0];
                var item = ctx.Properties.TryGetValue(SplitterStep.CurrentItemKey, out var v) ? v : null;
                processedItems.Add(item);
                return Task.CompletedTask;
            });

        var sut = new ComposedMessageProcessorStep(
            _ => new object[] { "x", "y" },
            processor,
            (results, _) => { aggregatorResults = results; return Task.CompletedTask; });

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("items seen by processor and results received by aggregator", () => (processedItems, aggregatorResults))
            .Then("processor saw 2 items and aggregator received 2 results", state =>
            {
                state.processedItems.Should().HaveCount(2);
                state.aggregatorResults.Should().NotBeNull().And.HaveCount(2);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ResultKey constant has expected value"), Fact]
    public async Task ResultKeyHasExpectedValue()
    {
        await Given("ComposedMessageProcessorStep.ResultKey constant", () => ComposedMessageProcessorStep.ResultKey)
            .Then("value is '__ComposedResult'", key =>
            {
                key.Should().Be("__ComposedResult");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null splitter throws ArgumentNullException"), Fact]
    public async Task NullSplitterThrows()
    {
        var processor = Substitute.For<IStep>();
        Exception? caught = null;
        try { _ = new ComposedMessageProcessorStep(null!, processor, (_, _) => Task.CompletedTask); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null splitter", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null processor throws ArgumentNullException"), Fact]
    public async Task NullProcessorThrows()
    {
        Exception? caught = null;
        try { _ = new ComposedMessageProcessorStep(_ => Enumerable.Empty<object>(), null!, (_, _) => Task.CompletedTask); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null processor", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null aggregator throws ArgumentNullException"), Fact]
    public async Task NullAggregatorThrows()
    {
        var processor = Substitute.For<IStep>();
        Exception? caught = null;
        try { _ = new ComposedMessageProcessorStep(_ => Enumerable.Empty<object>(), processor, null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null aggregator", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Empty items list calls aggregator with empty results"), Fact]
    public async Task EmptyItemsCallsAggregatorWithEmptyResults()
    {
        IReadOnlyList<object>? received = null;
        var processor = Substitute.For<IStep>();

        var sut = new ComposedMessageProcessorStep(
            _ => Enumerable.Empty<object>(),
            processor,
            (list, _) => { received = list; return Task.CompletedTask; });

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("list received by aggregator with empty items", () => received)
            .Then("aggregator received empty list", r =>
            {
                r.Should().NotBeNull().And.BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Processor __ProcessedItem key is used for aggregation result"), Fact]
    public async Task ProcessedItemKeyUsedForResult()
    {
        IReadOnlyList<object>? received = null;

        var processor = Substitute.For<IStep>();
        processor.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci =>
            {
                ((IWorkflowContext)ci[0]).Properties["__ProcessedItem"] = "done";
                return Task.CompletedTask;
            });

        var sut = new ComposedMessageProcessorStep(
            _ => new object[] { "raw" },
            processor,
            (list, _) => { received = list; return Task.CompletedTask; });

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("result received by aggregator", () => received)
            .Then("aggregated item is 'done' (from __ProcessedItem)", r =>
            {
                r.Should().ContainSingle().Which.Should().Be("done");
                return true;
            })
            .AssertPassed();
    }
}
