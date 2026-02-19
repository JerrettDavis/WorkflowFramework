using FluentAssertions;
using WorkflowFramework.Extensions.Integration.Composition;
using Xunit;

namespace WorkflowFramework.Tests.Integration;

public class CompositionPatternTests
{
    #region ScatterGather

    [Fact]
    public void ScatterGather_NullHandlers_Throws()
    {
        var act = () => new ScatterGatherStep(null!, (r, c) => Task.CompletedTask, TimeSpan.FromSeconds(1));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ScatterGather_NullAggregator_Throws()
    {
        var act = () => new ScatterGatherStep(Array.Empty<IStep>(), null!, TimeSpan.FromSeconds(1));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ScatterGather_AllRespond()
    {
        var h1 = new TestStep("H1", ctx => { ctx.Properties["__Result_H1"] = "r1"; return Task.CompletedTask; });
        var h2 = new TestStep("H2", ctx => { ctx.Properties["__Result_H2"] = "r2"; return Task.CompletedTask; });
        object?[]? results = null;
        var step = new ScatterGatherStep(
            new[] { h1, h2 },
            (r, c) => { results = r.ToArray(); return Task.CompletedTask; },
            TimeSpan.FromSeconds(5));
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task ScatterGather_HandlerException_ReturnsNull()
    {
        var h1 = new TestStep("H1", ctx => throw new Exception("boom"));
        var h2 = new TestStep("H2", ctx => { ctx.Properties["__Result_H2"] = "ok"; return Task.CompletedTask; });
        object?[]? results = null;
        var step = new ScatterGatherStep(
            new[] { h1, h2 },
            (r, c) => { results = r.ToArray(); return Task.CompletedTask; },
            TimeSpan.FromSeconds(5));
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        results.Should().HaveCount(2);
        results![0].Should().BeNull();
    }

    [Fact]
    public void ScatterGather_Name() => new ScatterGatherStep(Array.Empty<IStep>(), (r, c) => Task.CompletedTask, TimeSpan.FromSeconds(1)).Name.Should().Be("ScatterGather");

    #endregion

    #region Splitter

    [Fact]
    public void Splitter_NullSplitter_Throws()
    {
        var act = () => new SplitterStep(null!, new TestStep("P"));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Splitter_NullProcessor_Throws()
    {
        var act = () => new SplitterStep(ctx => Array.Empty<object>(), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Splitter_EmptyCollection_ProducesEmptyResults()
    {
        var step = new SplitterStep(ctx => Array.Empty<object>(), new TestStep("P"));
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        var results = (List<object?>)context.Properties[SplitterStep.ResultsKey]!;
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Splitter_SingleItem()
    {
        var processor = new TestStep("P", ctx =>
        {
            ctx.Properties["__ProcessedItem"] = $"processed_{ctx.Properties[SplitterStep.CurrentItemKey]}";
            return Task.CompletedTask;
        });
        var step = new SplitterStep(ctx => new object[] { "one" }, processor);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        var results = (List<object?>)context.Properties[SplitterStep.ResultsKey]!;
        results.Should().HaveCount(1);
        results[0].Should().Be("processed_one");
    }

    [Fact]
    public async Task Splitter_Parallel_ProcessesAll()
    {
        var count = 0;
        var processor = new TestStep("P", ctx => { Interlocked.Increment(ref count); return Task.CompletedTask; });
        var step = new SplitterStep(ctx => new object[] { 1, 2, 3 }, processor, parallel: true);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        count.Should().Be(3);
    }

    [Fact]
    public void Splitter_Name() => new SplitterStep(ctx => Array.Empty<object>(), new TestStep("P")).Name.Should().Be("Splitter");

    #endregion

    #region Aggregator

    [Fact]
    public void Aggregator_NullItemsSelector_Throws()
    {
        var act = () => new AggregatorStep(null!, (items, ctx) => Task.CompletedTask);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Aggregator_NullAggregateAction_Throws()
    {
        var act = () => new AggregatorStep(ctx => Array.Empty<object>(), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Aggregator_NoOptions_CollectsAll()
    {
        var collectedCount = 0;
        var step = new AggregatorStep(
            ctx => new object[] { 1, 2, 3, 4, 5 },
            (items, ctx) => { collectedCount = items.Count; return Task.CompletedTask; });
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        collectedCount.Should().Be(5);
    }

    [Fact]
    public async Task Aggregator_CountCompletion()
    {
        var collectedCount = 0;
        var options = new AggregatorOptions().CompleteAfterCount(3);
        var step = new AggregatorStep(
            ctx => new object[] { 1, 2, 3, 4, 5 },
            (items, ctx) => { collectedCount = items.Count; return Task.CompletedTask; },
            options);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        collectedCount.Should().Be(3);
    }

    [Fact]
    public async Task Aggregator_PredicateCompletion()
    {
        var collectedCount = 0;
        var options = new AggregatorOptions().CompleteWhen(items => items.Count >= 2);
        var step = new AggregatorStep(
            ctx => new object[] { 1, 2, 3, 4, 5 },
            (items, ctx) => { collectedCount = items.Count; return Task.CompletedTask; },
            options);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        collectedCount.Should().Be(2);
    }

    [Fact]
    public async Task Aggregator_TimeoutOption_CanBeSet()
    {
        var options = new AggregatorOptions().Timeout(TimeSpan.FromSeconds(5));
        // Timeout is stored but not directly used by AggregatorStep (it's sync collection)
        var step = new AggregatorStep(
            ctx => new object[] { 1 },
            (items, ctx) => Task.CompletedTask,
            options);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context); // Should not throw
    }

    [Fact]
    public void Aggregator_Name() => new AggregatorStep(ctx => Array.Empty<object>(), (i, c) => Task.CompletedTask).Name.Should().Be("Aggregator");

    #endregion

    #region Resequencer

    [Fact]
    public void Resequencer_NullItemsSelector_Throws()
    {
        var act = () => new ResequencerStep(null!, item => 0);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Resequencer_NullSequenceSelector_Throws()
    {
        var act = () => new ResequencerStep(ctx => Array.Empty<object>(), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Resequencer_ReordersOutOfSequence()
    {
        var step = new ResequencerStep(
            ctx => new object[] { 3, 1, 2 },
            item => (long)(int)item);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        var result = (List<object>)context.Properties[ResequencerStep.ResultKey]!;
        result.Select(x => (int)x).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Resequencer_Duplicates_Preserved()
    {
        var step = new ResequencerStep(
            ctx => new object[] { 2, 1, 2, 1 },
            item => (long)(int)item);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        var result = (List<object>)context.Properties[ResequencerStep.ResultKey]!;
        result.Select(x => (int)x).Should().Equal(1, 1, 2, 2);
    }

    [Fact]
    public void Resequencer_Name() => new ResequencerStep(ctx => Array.Empty<object>(), i => 0).Name.Should().Be("Resequencer");

    #endregion

    #region ComposedMessageProcessor

    [Fact]
    public void ComposedMessageProcessor_NullSplitter_Throws()
    {
        var act = () => new ComposedMessageProcessorStep(null!, new TestStep("P"), (i, c) => Task.CompletedTask);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ComposedMessageProcessor_NullProcessor_Throws()
    {
        var act = () => new ComposedMessageProcessorStep(ctx => Array.Empty<object>(), null!, (i, c) => Task.CompletedTask);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ComposedMessageProcessor_NullAggregator_Throws()
    {
        var act = () => new ComposedMessageProcessorStep(ctx => Array.Empty<object>(), new TestStep("P"), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ComposedMessageProcessor_FullPipeline()
    {
        var processor = new TestStep("Double", ctx =>
        {
            var item = (int)ctx.Properties[SplitterStep.CurrentItemKey]!;
            ctx.Properties["__ProcessedItem"] = item * 2;
            return Task.CompletedTask;
        });
        object? sum = null;
        var step = new ComposedMessageProcessorStep(
            ctx => new object[] { 1, 2, 3 },
            processor,
            (items, ctx) => { sum = items.Cast<int>().Sum(); return Task.CompletedTask; });
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        sum.Should().Be(12);
    }

    [Fact]
    public void ComposedMessageProcessor_Name() => new ComposedMessageProcessorStep(ctx => Array.Empty<object>(), new TestStep("P"), (i, c) => Task.CompletedTask).Name.Should().Be("ComposedMessageProcessor");

    #endregion

    #region ProcessManager

    [Fact]
    public void ProcessManager_NullStateSelector_Throws()
    {
        var act = () => new ProcessManagerStep(null!, new Dictionary<string, IStep>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ProcessManager_NullHandlers_Throws()
    {
        var act = () => new ProcessManagerStep(ctx => "s", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ProcessManager_StateTransitions()
    {
        var log = new List<string>();
        var handlers = new Dictionary<string, IStep>
        {
            ["init"] = new TestStep("Init", ctx => { log.Add("init"); ctx.Properties["state"] = "process"; return Task.CompletedTask; }),
            ["process"] = new TestStep("Process", ctx => { log.Add("process"); ctx.Properties["state"] = "done"; return Task.CompletedTask; }),
        };
        var step = new ProcessManagerStep(ctx => (string)(ctx.Properties.TryGetValue("state", out var s) ? s! : "init"), handlers);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        log.Should().Equal("init", "process");
    }

    [Fact]
    public async Task ProcessManager_TerminalState_Stops()
    {
        var count = 0;
        var handlers = new Dictionary<string, IStep>
        {
            ["a"] = new TestStep("A", ctx => { count++; ctx.Properties["state"] = "terminal"; return Task.CompletedTask; }),
        };
        var step = new ProcessManagerStep(ctx => (string)(ctx.Properties.TryGetValue("state", out var s) ? s! : "a"), handlers);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        count.Should().Be(1);
    }

    [Fact]
    public async Task ProcessManager_NoStateChange_Stops()
    {
        var count = 0;
        var handlers = new Dictionary<string, IStep>
        {
            ["a"] = new TestStep("A", ctx => { count++; return Task.CompletedTask; }),
        };
        var step = new ProcessManagerStep(ctx => "a", handlers);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        count.Should().Be(1);
    }

    [Fact]
    public async Task ProcessManager_StopsOnAbort()
    {
        var log = new List<string>();
        var handlers = new Dictionary<string, IStep>
        {
            ["a"] = new TestStep("A", ctx => { log.Add("a"); ctx.IsAborted = true; ctx.Properties["state"] = "b"; return Task.CompletedTask; }),
            ["b"] = new TestStep("B", ctx => { log.Add("b"); return Task.CompletedTask; }),
        };
        var step = new ProcessManagerStep(ctx => (string)(ctx.Properties.TryGetValue("state", out var s) ? s! : "a"), handlers);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        log.Should().Equal("a");
    }

    [Fact]
    public void ProcessManager_Name() => new ProcessManagerStep(ctx => "s", new Dictionary<string, IStep>()).Name.Should().Be("ProcessManager");

    #endregion

    #region Helpers

    private sealed class TestStep(string name, Func<IWorkflowContext, Task>? action = null) : IStep
    {
        public string Name { get; } = name;
        public Task ExecuteAsync(IWorkflowContext context) => action?.Invoke(context) ?? Task.CompletedTask;
    }

    #endregion
}
