using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Reactive.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Reactive.Tests;

[Feature("IAsyncStep and AsyncStepAdapter")]
public class IAsyncStepScenarios : ReactiveTestBase
{
    public IAsyncStepScenarios(ITestOutputHelper output) : base(output) { }

    // --------------- helpers ---------------

    private static WorkflowContext MakeContext(CancellationToken ct = default) => new(ct);

    private sealed class CountingStep : IAsyncStep<int>
    {
        private readonly int _count;
        private readonly int _failAt;

        public CountingStep(int count, int failAt = -1)
        {
            _count = count;
            _failAt = failAt;
        }

        public string Name => "counting-step";

        public async IAsyncEnumerable<int> ExecuteStreamingAsync(IWorkflowContext context)
        {
            for (int i = 0; i < _count; i++)
            {
                if (i == _failAt)
                    throw new InvalidOperationException($"Fault at item {i}");
                yield return i;
                await Task.Yield();
            }
        }
    }

    // --------------- scenarios ---------------

    [Scenario("CollectAsync gathers all items streamed by an IAsyncStep"), Fact]
    public async Task CollectAsyncGathersAllItems()
    {
        var step = new CountingStep(5);
        var context = MakeContext();

        var results = await step.CollectAsync(context);

        await Given("a step that yields 5 items", () => results)
            .Then("all 5 items are collected in order", items =>
            {
                items.Should().Equal(0, 1, 2, 3, 4);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AsyncStepAdapter stores collected results in context Properties"), Fact]
    public async Task AsyncStepAdapterStoresResultsInProperties()
    {
        var innerStep = new CountingStep(3);
        var adapter = new AsyncStepAdapter<int>(innerStep);
        var context = MakeContext();

        await adapter.ExecuteAsync(context);

        await Given("an AsyncStepAdapter wrapping a 3-item step", () => context)
            .Then("results are stored under the default key", ctx =>
            {
                var key = "counting-step.Results";
                ctx.Properties.Should().ContainKey(key);
                var stored = ctx.Properties[key].Should().BeAssignableTo<List<int>>().Subject;
                stored.Should().Equal(0, 1, 2);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AsyncStepAdapter uses custom results key when provided"), Fact]
    public async Task AsyncStepAdapterUsesCustomKey()
    {
        const string customKey = "my-results";
        var innerStep = new CountingStep(2);
        var adapter = new AsyncStepAdapter<int>(innerStep, customKey);
        var context = MakeContext();

        await adapter.ExecuteAsync(context);

        await Given("an AsyncStepAdapter with a custom key", () => context)
            .Then("results are stored under the custom key", ctx =>
            {
                ctx.Properties.Should().ContainKey(customKey);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CollectAsync propagates fault thrown by the underlying step"), Fact]
    public async Task CollectAsyncPropagatesFault()
    {
        var faultyStep = new CountingStep(5, failAt: 2);
        var context = MakeContext();

        Func<Task> act = () => faultyStep.CollectAsync(context);

        await Given("a step that throws at item 2", () => act)
            .Then("CollectAsync propagates the exception", fn =>
            {
                fn.Should().ThrowAsync<InvalidOperationException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CollectAsync respects cancellation token"), Fact]
    public async Task CollectAsyncRespectsCancellation()
    {
        // A step that yields many items
        var step = new CountingStep(1000);
        using var cts = new CancellationTokenSource();

        // cancel immediately
        cts.Cancel();
        var context = MakeContext(cts.Token);

        Func<Task> act = () => step.CollectAsync(context, cts.Token);

        await Given("a cancelled context", () => act)
            .Then("CollectAsync throws OperationCanceledException", fn =>
            {
                fn.Should().ThrowAsync<OperationCanceledException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AsyncStepAdapter requires non-null inner step"), Fact]
    public async Task AsyncStepAdapterRequiresNonNullInnerStep()
    {
        Exception? caught = null;
        try { _ = new AsyncStepAdapter<int>(null!); }
        catch (Exception ex) { caught = ex; }

        await Given("a null inner step", () => caught)
            .Then("constructor throws ArgumentNullException", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ForEachAsync invokes the callback for every streamed item"), Fact]
    public async Task ForEachAsyncInvokesCallbackPerItem()
    {
        var step = new CountingStep(4);
        var context = MakeContext();
        var seen = new List<int>();

        await step.ForEachAsync(context, item =>
        {
            seen.Add(item);
            return Task.CompletedTask;
        });

        await Given("a 4-item step and ForEachAsync with a collecting callback", () => seen)
            .Then("callback was invoked for each item in order", items =>
            {
                items.Should().Equal(0, 1, 2, 3);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ForEachAsync propagates fault from underlying step"), Fact]
    public async Task ForEachAsyncPropagatesFault()
    {
        var faultyStep = new CountingStep(5, failAt: 1);
        var context = MakeContext();

        Func<Task> act = () => faultyStep.ForEachAsync(context, _ => Task.CompletedTask);

        await Given("a step that faults at item 1 in ForEachAsync", () => act)
            .Then("the exception propagates from ForEachAsync", fn =>
            {
                fn.Should().ThrowAsync<InvalidOperationException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AsyncStepAdapter uses the step name as default property key prefix"), Fact]
    public async Task AsyncStepAdapterDefaultKeyUsesStepName()
    {
        var innerStep = new CountingStep(1);
        var adapter = new AsyncStepAdapter<int>(innerStep);

        await Given("an AsyncStepAdapter with no explicit key", () => adapter)
            .Then("Name matches the inner step name", a =>
            {
                a.Name.Should().Be("counting-step");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CollectAsync returns empty list for a step that yields no items"), Fact]
    public async Task CollectAsyncEmptyStep()
    {
        var step = new CountingStep(0);
        var context = MakeContext();

        var results = await step.CollectAsync(context);

        await Given("a step that yields 0 items", () => results)
            .Then("CollectAsync returns an empty list", items =>
            {
                items.Should().BeEmpty();
                return true;
            })
            .AssertPassed();
    }
}
