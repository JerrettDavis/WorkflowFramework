using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Composition;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Composition;

[Feature("AggregatorStep — characterization (Phase G.2)")]
public class AggregatorStepScenarios : TinyBddTestBase
{
    public AggregatorStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("AggregatorStep Name returns 'Aggregator'"), Fact]
    public async Task NameIsAggregator()
    {
        var sut = new AggregatorStep(_ => Enumerable.Empty<object>(), (_, _) => Task.CompletedTask);

        await Given("AggregatorStep instance", () => sut)
            .Then("Name is 'Aggregator'", s =>
            {
                s.Name.Should().Be("Aggregator");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Aggregator collects all items when no completion condition is set"), Fact]
    public async Task NoConditionCollectsAllItems()
    {
        IReadOnlyList<object>? collected = null;
        var items = new object[] { "a", "b", "c" };
        var sut = new AggregatorStep(
            _ => items,
            (list, _) => { collected = list; return Task.CompletedTask; });

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("list passed to aggregator with no completion condition", () => collected)
            .Then("all 3 items were collected", c =>
            {
                c.Should().NotBeNull().And.HaveCount(3).And.ContainInOrder("a", "b", "c");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CompleteAfterCount limits collected items"), Fact]
    public async Task CompleteAfterCountLimitsItems()
    {
        IReadOnlyList<object>? collected = null;
        var items = new object[] { 1, 2, 3, 4, 5 };
        var options = new AggregatorOptions().CompleteAfterCount(3);
        var sut = new AggregatorStep(
            _ => items,
            (list, _) => { collected = list; return Task.CompletedTask; },
            options);

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("collected items with CompleteAfterCount(3)", () => collected)
            .Then("exactly 3 items collected", c =>
            {
                c.Should().NotBeNull().And.HaveCount(3);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CompleteWhen stops collecting when predicate is satisfied"), Fact]
    public async Task CompleteWhenStopsOnPredicate()
    {
        IReadOnlyList<object>? collected = null;
        var items = new object[] { 1, 2, 3, 4, 5 };
        // Complete when sum of collected ints >= 6  (1+2+3=6)
        var options = new AggregatorOptions().CompleteWhen(list => list.Cast<int>().Sum() >= 6);
        var sut = new AggregatorStep(
            _ => items,
            (list, _) => { collected = list; return Task.CompletedTask; },
            options);

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("collected items where predicate stops at sum>=6", () => collected)
            .Then("3 items were collected (1+2+3=6)", c =>
            {
                c.Should().NotBeNull().And.HaveCount(3);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null itemsSelector throws ArgumentNullException"), Fact]
    public async Task NullItemsSelectorThrows()
    {
        Exception? caught = null;
        try { _ = new AggregatorStep(null!, (_, _) => Task.CompletedTask); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null items selector", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null aggregateAction throws ArgumentNullException"), Fact]
    public async Task NullAggregateActionThrows()
    {
        Exception? caught = null;
        try { _ = new AggregatorStep(_ => Enumerable.Empty<object>(), null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null aggregate action", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ResultKey constant has expected value"), Fact]
    public async Task ResultKeyHasExpectedValue()
    {
        await Given("AggregatorStep.ResultKey constant", () => AggregatorStep.ResultKey)
            .Then("value is '__AggregatorResult'", key =>
            {
                key.Should().Be("__AggregatorResult");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Aggregator receives context to store result"), Fact]
    public async Task AggregatorReceivesContext()
    {
        var ctx = new WorkflowContext();
        var items = new object[] { "x" };
        var sut = new AggregatorStep(
            _ => items,
            (list, c) =>
            {
                c.Properties[AggregatorStep.ResultKey] = string.Join(",", list.Cast<string>());
                return Task.CompletedTask;
            });

        await sut.ExecuteAsync(ctx);

        await Given("context after aggregation", () => ctx)
            .Then("result key contains 'x'", c =>
            {
                c.Properties[AggregatorStep.ResultKey].Should().Be("x");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Empty items list calls aggregator with empty collection"), Fact]
    public async Task EmptyItemsCallsAggregatorWithEmptyList()
    {
        IReadOnlyList<object>? received = null;
        var sut = new AggregatorStep(
            _ => Enumerable.Empty<object>(),
            (list, _) => { received = list; return Task.CompletedTask; });

        await sut.ExecuteAsync(new WorkflowContext());

        await Given("list received by aggregator with empty source", () => received)
            .Then("aggregator was called with empty list", r =>
            {
                r.Should().NotBeNull().And.BeEmpty();
                return true;
            })
            .AssertPassed();
    }
}
