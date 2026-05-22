using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Composition;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Composition;

[Feature("ResequencerStep — characterization (Phase G.2)")]
public class ResequencerStepScenarios : TinyBddTestBase
{
    public ResequencerStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("ResequencerStep Name returns 'Resequencer'"), Fact]
    public async Task NameIsResequencer()
    {
        var sut = new ResequencerStep(_ => Enumerable.Empty<object>(), _ => 0L);

        await Given("ResequencerStep instance", () => sut)
            .Then("Name is 'Resequencer'", s =>
            {
                s.Name.Should().Be("Resequencer");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Out-of-order items are sorted by sequence number"), Fact]
    public async Task ItemsAreSortedBySequenceNumber()
    {
        var items = new (long Seq, string Value)[] { (3L, "c"), (1L, "a"), (2L, "b") };
        var ctx = new WorkflowContext();

        var sut = new ResequencerStep(
            _ => items.Cast<object>(),
            item => ((ValueTuple<long, string>)item).Item1);

        await sut.ExecuteAsync(ctx);

        await Given("resequenced items from context", () => ctx)
            .Then("items are ordered a, b, c (seq 1, 2, 3)", c =>
            {
                var result = c.Properties[ResequencerStep.ResultKey] as List<object>;
                result.Should().NotBeNull().And.HaveCount(3);
                var seq = result!.Cast<(long, string)>().Select(x => x.Item2).ToList();
                seq.Should().ContainInOrder("a", "b", "c");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Already-ordered items remain in same order"), Fact]
    public async Task AlreadyOrderedItemsUnchanged()
    {
        var items = new object[] { 10L, 20L, 30L };
        var ctx = new WorkflowContext();

        var sut = new ResequencerStep(_ => items, item => (long)item);
        await sut.ExecuteAsync(ctx);

        await Given("result key from context after resequencing already-ordered items", () => ctx)
            .Then("items remain in original order", c =>
            {
                var result = c.Properties[ResequencerStep.ResultKey] as List<object>;
                result.Should().ContainInOrder(10L, 20L, 30L);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Empty items list stores empty ResultKey"), Fact]
    public async Task EmptyItemsStoresEmptyResult()
    {
        var ctx = new WorkflowContext();
        var sut = new ResequencerStep(_ => Enumerable.Empty<object>(), _ => 0L);

        await sut.ExecuteAsync(ctx);

        await Given("context after resequencing empty list", () => ctx)
            .Then("ResultKey is set to empty list", c =>
            {
                c.Properties.Should().ContainKey(ResequencerStep.ResultKey);
                var result = c.Properties[ResequencerStep.ResultKey] as List<object>;
                result.Should().NotBeNull().And.BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ResultKey constant has expected value"), Fact]
    public async Task ResultKeyHasExpectedValue()
    {
        await Given("ResequencerStep.ResultKey constant", () => ResequencerStep.ResultKey)
            .Then("value is '__ResequencerResult'", key =>
            {
                key.Should().Be("__ResequencerResult");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null itemsSelector throws ArgumentNullException"), Fact]
    public async Task NullItemsSelectorThrows()
    {
        Exception? caught = null;
        try { _ = new ResequencerStep(null!, _ => 0L); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null items selector", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null sequenceSelector throws ArgumentNullException"), Fact]
    public async Task NullSequenceSelectorThrows()
    {
        Exception? caught = null;
        try { _ = new ResequencerStep(_ => Enumerable.Empty<object>(), null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null sequence selector", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Resequencer stores result synchronously (Task.CompletedTask)"), Fact]
    public async Task ResequencerIsSynchronous()
    {
        var sut = new ResequencerStep(_ => Enumerable.Empty<object>(), _ => 0L);
        var task = sut.ExecuteAsync(new WorkflowContext());

        await Given("task returned by resequencer", () => task)
            .Then("task is already completed", t =>
            {
                t.IsCompletedSuccessfully.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }
}
