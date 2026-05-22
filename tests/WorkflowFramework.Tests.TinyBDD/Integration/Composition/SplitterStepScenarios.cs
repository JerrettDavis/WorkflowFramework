using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Composition;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Composition;

[Feature("SplitterStep — characterization (Phase G.2)")]
public class SplitterStepScenarios : TinyBddTestBase
{
    public SplitterStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("SplitterStep Name returns 'Splitter'"), Fact]
    public async Task NameIsSplitter()
    {
        var processor = Substitute.For<IStep>();
        var sut = new SplitterStep(_ => Enumerable.Empty<object>(), processor);

        await Given("SplitterStep instance", () => sut)
            .Then("Name is 'Splitter'", s =>
            {
                s.Name.Should().Be("Splitter");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Sequential split sets CurrentItemKey for each item and stores results"), Fact]
    public async Task SequentialSplitSetsCurrentItem()
    {
        var processedItems = new List<object?>();
        var processor = Substitute.For<IStep>();
        processor.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci =>
            {
                var ctx = (IWorkflowContext)ci[0];
                processedItems.Add(ctx.Properties.TryGetValue(SplitterStep.CurrentItemKey, out var item) ? item : null);
                return Task.CompletedTask;
            });

        var items = new object[] { "a", "b", "c" };
        var sut = new SplitterStep(_ => items, processor, parallel: false);

        var ctx = new WorkflowContext();
        await sut.ExecuteAsync(ctx);

        await Given("items seen by processor during sequential split", () => (processedItems, ctx))
            .Then("processor saw all 3 items and ResultsKey is set", state =>
            {
                state.processedItems.Should().HaveCount(3).And.ContainInOrder("a", "b", "c");
                state.ctx.Properties.Should().ContainKey(SplitterStep.ResultsKey);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Parallel split executes processor for all items"), Fact]
    public async Task ParallelSplitRunsAll()
    {
        var count = 0;
        var processor = Substitute.For<IStep>();
        processor.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(_ => { Interlocked.Increment(ref count); return Task.CompletedTask; });

        var items = new object[] { 1, 2, 3, 4 };
        var sut = new SplitterStep(_ => items, processor, parallel: true);

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("processor call count after parallel split with 4 items", () => count)
            .Then("processor ran 4 times", c =>
            {
                c.Should().Be(4);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Empty items list stores empty ResultsKey"), Fact]
    public async Task EmptyItemsStoresEmptyResults()
    {
        var processor = Substitute.For<IStep>();
        var sut = new SplitterStep(_ => Enumerable.Empty<object>(), processor, parallel: false);

        var ctx = new WorkflowContext();
        await sut.ExecuteAsync(ctx);

        await Given("context after splitting empty items list", () => ctx)
            .Then("ResultsKey is set to empty list", c =>
            {
                c.Properties.Should().ContainKey(SplitterStep.ResultsKey);
                var results = c.Properties[SplitterStep.ResultsKey] as List<object?>;
                results.Should().NotBeNull().And.BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CurrentItemKey constant has expected value"), Fact]
    public async Task CurrentItemKeyHasExpectedValue()
    {
        await Given("SplitterStep.CurrentItemKey constant", () => SplitterStep.CurrentItemKey)
            .Then("value is '__SplitterCurrentItem'", key =>
            {
                key.Should().Be("__SplitterCurrentItem");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ResultsKey constant has expected value"), Fact]
    public async Task ResultsKeyHasExpectedValue()
    {
        await Given("SplitterStep.ResultsKey constant", () => SplitterStep.ResultsKey)
            .Then("value is '__SplitterResults'", key =>
            {
                key.Should().Be("__SplitterResults");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null splitter throws ArgumentNullException"), Fact]
    public async Task NullSplitterThrows()
    {
        var processor = Substitute.For<IStep>();
        Exception? caught = null;
        try { _ = new SplitterStep(null!, processor); }
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
        try { _ = new SplitterStep(_ => Enumerable.Empty<object>(), null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null processor", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Processed item from '__ProcessedItem' key is stored in results"), Fact]
    public async Task ProcessedItemKeyIsUsedForResult()
    {
        var processor = Substitute.For<IStep>();
        processor.ExecuteAsync(Arg.Any<IWorkflowContext>())
            .Returns(ci =>
            {
                ((IWorkflowContext)ci[0]).Properties["__ProcessedItem"] = "transformed";
                return Task.CompletedTask;
            });

        var sut = new SplitterStep(_ => new object[] { "original" }, processor);
        var ctx = new WorkflowContext();
        await sut.ExecuteAsync(ctx);

        await Given("context after processor sets __ProcessedItem", () => ctx)
            .Then("results contain 'transformed'", c =>
            {
                var results = c.Properties[SplitterStep.ResultsKey] as List<object?>;
                results.Should().ContainSingle().Which.Should().Be("transformed");
                return true;
            })
            .AssertPassed();
    }
}
