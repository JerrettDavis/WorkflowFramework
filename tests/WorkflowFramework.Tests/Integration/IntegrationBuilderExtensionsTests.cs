using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Builder;
using WorkflowFramework.Extensions.Integration.Abstractions;
using WorkflowFramework.Extensions.Integration.Builder;
using WorkflowFramework.Extensions.Integration.Composition;
using Xunit;

namespace WorkflowFramework.Tests.Integration;

public class IntegrationBuilderExtensionsTests
{
    [Fact]
    public async Task Route_AddsContentBasedRouterStep()
    {
        var executed = false;
        var workflow = new WorkflowBuilder()
            .WithName("Test")
            .Route(new (Func<IWorkflowContext, bool>, IStep)[]
            {
                (ctx => true, new TestStep("A", ctx => { executed = true; return Task.CompletedTask; })),
            })
            .Build();
        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task Filter_AddsMessageFilterStep()
    {
        var workflow = new WorkflowBuilder()
            .WithName("Test")
            .Filter(ctx => false)
            .Build();
        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);
        context.IsAborted.Should().BeTrue();
    }

    [Fact]
    public async Task DynamicRoute_AddsDynamicRouterStep()
    {
        var count = 0;
        var step = new TestStep("Inc", ctx => { count++; ctx.Properties["c"] = count; return Task.CompletedTask; });
        var workflow = new WorkflowBuilder()
            .WithName("Test")
            .DynamicRoute(ctx => ctx.Properties.TryGetValue("c", out var v) && (int)v! >= 1 ? null : step)
            .Build();
        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);
        count.Should().Be(1);
    }

    [Fact]
    public async Task RecipientList_AddsRecipientListStep()
    {
        var log = new List<string>();
        var workflow = new WorkflowBuilder()
            .WithName("Test")
            .RecipientList(ctx => new IStep[]
            {
                new TestStep("A", c => { log.Add("A"); return Task.CompletedTask; }),
            })
            .Build();
        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);
        log.Should().Equal("A");
    }

    [Fact]
    public async Task Split_AddsSplitterStep()
    {
        var processor = new TestStep("P");
        var workflow = new WorkflowBuilder()
            .WithName("Test")
            .Split(ctx => new object[] { 1, 2 }, processor)
            .Build();
        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);
        context.Properties.Should().ContainKey(SplitterStep.ResultsKey);
    }

    [Fact]
    public async Task Aggregate_AddsAggregatorStep()
    {
        var collected = 0;
        var workflow = new WorkflowBuilder()
            .WithName("Test")
            .Step("setup", ctx => { ctx.Properties["items"] = new object[] { 1, 2, 3 }; return Task.CompletedTask; })
            .Aggregate(
                ctx => (IEnumerable<object>)ctx.Properties["items"]!,
                (items, ctx) => { collected = items.Count; return Task.CompletedTask; },
                opts => opts.CompleteAfterCount(2))
            .Build();
        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);
        collected.Should().Be(2);
    }

    [Fact]
    public async Task ScatterGather_AddsScatterGatherStep()
    {
        var workflow = new WorkflowBuilder()
            .WithName("Test")
            .ScatterGather(
                new[] { new TestStep("H1") },
                (r, c) => Task.CompletedTask,
                TimeSpan.FromSeconds(5))
            .Build();
        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);
    }

    [Fact]
    public async Task Enrich_AddsContentEnricherStep()
    {
        var workflow = new WorkflowBuilder()
            .WithName("Test")
            .Enrich(ctx => { ctx.Properties["enriched"] = true; return Task.CompletedTask; })
            .Build();
        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);
        context.Properties["enriched"].Should().Be(true);
    }

    [Fact]
    public async Task WireTap_AddsWireTapStep()
    {
        var tapped = false;
        var workflow = new WorkflowBuilder()
            .WithName("Test")
            .WireTap(ctx => { tapped = true; return Task.CompletedTask; })
            .Build();
        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);
        tapped.Should().BeTrue();
    }

    [Fact]
    public async Task WithDeadLetter_AddsDeadLetterStep()
    {
        var store = Substitute.For<IDeadLetterStore>();
        var inner = new TestStep("fail", ctx => throw new Exception("err"));
        var workflow = new WorkflowBuilder()
            .WithName("Test")
            .WithDeadLetter(store, inner)
            .Build();
        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);
        await store.Received(1).SendAsync(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClaimCheck_AddsClaimCheckStep()
    {
        var store = Substitute.For<IClaimCheckStore>();
        store.StoreAsync(Arg.Any<object>(), Arg.Any<CancellationToken>()).Returns("ticket-1");
        var workflow = new WorkflowBuilder()
            .WithName("Test")
            .Step("setup", ctx => { ctx.Properties["payload"] = "data"; return Task.CompletedTask; })
            .ClaimCheck(store, ctx => ctx.Properties["payload"]!)
            .Build();
        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);
        await store.Received(1).StoreAsync("data", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClaimRetrieve_AddsClaimRetrieveStep()
    {
        var store = Substitute.For<IClaimCheckStore>();
        store.StoreAsync(Arg.Any<object>(), Arg.Any<CancellationToken>()).Returns("ticket-1");
        store.RetrieveAsync("ticket-1", Arg.Any<CancellationToken>()).Returns((object)"payload");
        var workflow = new WorkflowBuilder()
            .WithName("Test")
            .Step("setup", ctx => { ctx.Properties["payload"] = "data"; return Task.CompletedTask; })
            .ClaimCheck(store, ctx => ctx.Properties["payload"]!)
            .ClaimRetrieve(store, "result")
            .Build();
        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);
        context.Properties["result"].Should().Be("payload");
    }

    [Fact]
    public async Task Resequence_AddsResequencerStep()
    {
        var workflow = new WorkflowBuilder()
            .WithName("Test")
            .Step("setup", ctx => { ctx.Properties["items"] = new object[] { 3, 1, 2 }; return Task.CompletedTask; })
            .Resequence(ctx => (IEnumerable<object>)ctx.Properties["items"]!, item => (long)(int)item)
            .Build();
        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);
        context.Properties.Should().ContainKey("__ResequencerResult");
    }

    #region Helpers

    private sealed class TestStep : IStep
    {
        private readonly Func<IWorkflowContext, Task>? _action;
        public TestStep(string name, Func<IWorkflowContext, Task>? action = null) { Name = name; _action = action; }
        public string Name { get; }
        public Task ExecuteAsync(IWorkflowContext context) => _action?.Invoke(context) ?? Task.CompletedTask;
    }

    #endregion
}
