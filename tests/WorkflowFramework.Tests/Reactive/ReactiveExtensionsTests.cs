using FluentAssertions;
using WorkflowFramework.Extensions.Reactive;
using Xunit;
using System.Runtime.CompilerServices;

namespace WorkflowFramework.Tests.Reactive;

public class ReactiveExtensionsTests
{
    [Fact]
    public async Task CollectAsync_CollectsAllResults()
    {
        var step = new TestAsyncStep("test", new[] { 1, 2, 3 });
        var context = new WorkflowContext();
        var results = await step.CollectAsync(context);
        results.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task CollectAsync_EmptyStream_ReturnsEmpty()
    {
        var step = new TestAsyncStep("test", Array.Empty<int>());
        var context = new WorkflowContext();
        var results = await step.CollectAsync(context);
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task CollectAsync_Cancellation_Stops()
    {
        var cts = new CancellationTokenSource();
        var step = new CancellableAsyncStep("test", cts);
        var context = new WorkflowContext();
        cts.CancelAfter(50);
        var act = () => step.CollectAsync(context, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ForEachAsync_InvokesCallbackForEachItem()
    {
        var step = new TestAsyncStep("test", new[] { 10, 20, 30 });
        var context = new WorkflowContext();
        var collected = new List<int>();
        await step.ForEachAsync(context, async item =>
        {
            collected.Add(item);
        });
        collected.Should().Equal(10, 20, 30);
    }

    [Fact]
    public async Task AsyncStepAdapter_CollectsAndStoresResults()
    {
        var inner = new TestAsyncStep("test", new[] { 1, 2 });
        var adapter = new AsyncStepAdapter<int>(inner);
        adapter.Name.Should().Be("test");
        var context = new WorkflowContext();
        await adapter.ExecuteAsync(context);
        context.Properties["test.Results"].Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public async Task AsyncStepAdapter_CustomResultsKey()
    {
        var inner = new TestAsyncStep("test", new[] { 1 });
        var adapter = new AsyncStepAdapter<int>(inner, "myResults");
        var context = new WorkflowContext();
        await adapter.ExecuteAsync(context);
        context.Properties["myResults"].Should().BeEquivalentTo(new[] { 1 });
    }

    [Fact]
    public void AsyncStepAdapter_NullInner_Throws()
    {
        var act = () => new AsyncStepAdapter<int>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    private sealed class TestAsyncStep(string name, int[] items) : IAsyncStep<int>
    {
        public string Name { get; } = name;

        public async IAsyncEnumerable<int> ExecuteStreamingAsync(IWorkflowContext context)
        {
            foreach (var item in items)
            {
                await Task.Yield();
                yield return item;
            }
        }
    }

    private sealed class CancellableAsyncStep(string name, CancellationTokenSource cts) : IAsyncStep<int>
    {
        public string Name { get; } = name;

        public async IAsyncEnumerable<int> ExecuteStreamingAsync(IWorkflowContext context)
        {
            for (var i = 0; ; i++)
            {
                cts.Token.ThrowIfCancellationRequested();
                await Task.Delay(20, cts.Token);
                yield return i;
            }
        }
    }
}
