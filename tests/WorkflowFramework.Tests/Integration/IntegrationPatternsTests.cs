using WorkflowFramework.Builder;
using WorkflowFramework.Extensions.Integration.Abstractions;
using WorkflowFramework.Extensions.Integration.Channel;
using WorkflowFramework.Extensions.Integration.Composition;
using WorkflowFramework.Extensions.Integration.Endpoint;
using WorkflowFramework.Extensions.Integration.Routing;
using WorkflowFramework.Extensions.Integration.Transformation;
using WorkflowFramework.Extensions.Integration.Builder;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace WorkflowFramework.Tests.Integration;

public class IntegrationPatternsTests
{
    #region Content-Based Router

    [Fact]
    public async Task ContentBasedRouter_RoutesToMatchingBranch()
    {
        var executedStep = "";
        var stepA = new TestStep("A", ctx => { executedStep = "A"; return Task.CompletedTask; });
        var stepB = new TestStep("B", ctx => { executedStep = "B"; return Task.CompletedTask; });

        var router = new ContentBasedRouterStep(new (Func<IWorkflowContext, bool>, IStep)[]
        {
            (ctx => ctx.Properties.ContainsKey("typeA"), stepA),
            (ctx => ctx.Properties.ContainsKey("typeB"), stepB),
        });

        var context = new WorkflowContext();
        context.Properties["typeB"] = true;

        await router.ExecuteAsync(context);

        executedStep.Should().Be("B");
    }

    [Fact]
    public async Task ContentBasedRouter_UsesDefaultWhenNoMatch()
    {
        var executedStep = "";
        var defaultStep = new TestStep("Default", ctx => { executedStep = "Default"; return Task.CompletedTask; });

        var router = new ContentBasedRouterStep(
            Array.Empty<(Func<IWorkflowContext, bool>, IStep)>(),
            defaultStep);

        var context = new WorkflowContext();
        await router.ExecuteAsync(context);

        executedStep.Should().Be("Default");
    }

    [Fact]
    public async Task ContentBasedRouter_MultipleRoutes_FirstMatchWins()
    {
        var executed = new List<string>();
        var stepA = new TestStep("A", ctx => { executed.Add("A"); return Task.CompletedTask; });
        var stepB = new TestStep("B", ctx => { executed.Add("B"); return Task.CompletedTask; });

        var router = new ContentBasedRouterStep(new (Func<IWorkflowContext, bool>, IStep)[]
        {
            (ctx => true, stepA),
            (ctx => true, stepB),
        });

        var context = new WorkflowContext();
        await router.ExecuteAsync(context);

        executed.Should().ContainSingle().Which.Should().Be("A");
    }

    #endregion

    #region Message Filter

    [Fact]
    public async Task MessageFilter_PassesMatchingMessages()
    {
        var filter = new MessageFilterStep(ctx => ctx.Properties.ContainsKey("valid"));
        var context = new WorkflowContext();
        context.Properties["valid"] = true;

        await filter.ExecuteAsync(context);

        context.IsAborted.Should().BeFalse();
    }

    [Fact]
    public async Task MessageFilter_AbortsNonMatchingMessages()
    {
        var filter = new MessageFilterStep(ctx => ctx.Properties.ContainsKey("valid"));
        var context = new WorkflowContext();

        await filter.ExecuteAsync(context);

        context.IsAborted.Should().BeTrue();
    }

    #endregion

    #region Dynamic Router

    [Fact]
    public async Task DynamicRouter_ExecutesUntilNullReturned()
    {
        var callCount = 0;
        var step = new TestStep("Inc", ctx =>
        {
            callCount++;
            ctx.Properties["count"] = callCount;
            return Task.CompletedTask;
        });

        var router = new DynamicRouterStep(ctx =>
        {
            var count = ctx.Properties.TryGetValue("count", out var c) ? (int)c! : 0;
            return count < 3 ? step : null;
        });

        var context = new WorkflowContext();
        await router.ExecuteAsync(context);

        callCount.Should().Be(3);
    }

    #endregion

    #region Splitter + Aggregator Round-Trip

    [Fact]
    public async Task SplitterAggregator_RoundTrip()
    {
        var processed = new List<object>();
        var processor = new TestStep("Process", ctx =>
        {
            var item = ctx.Properties[SplitterStep.CurrentItemKey]!;
            var result = $"processed_{item}";
            processed.Add(result);
            ctx.Properties["__ProcessedItem"] = result;
            return Task.CompletedTask;
        });

        var splitter = new SplitterStep(
            ctx => (IEnumerable<object>)ctx.Properties["items"]!,
            processor);

        var context = new WorkflowContext();
        context.Properties["items"] = new object[] { "a", "b", "c" };

        await splitter.ExecuteAsync(context);

        var results = (List<object?>)context.Properties[SplitterStep.ResultsKey]!;
        results.Should().HaveCount(3);
        results.Should().Contain("processed_a");
        results.Should().Contain("processed_b");
        results.Should().Contain("processed_c");
    }

    #endregion

    #region Aggregator Completion Conditions

    [Fact]
    public async Task Aggregator_CompletesAfterCount()
    {
        var collectedCount = 0;
        var options = new AggregatorOptions().CompleteAfterCount(2);

        var aggregator = new AggregatorStep(
            ctx => (IEnumerable<object>)ctx.Properties["items"]!,
            (items, ctx) => { collectedCount = items.Count; return Task.CompletedTask; },
            options);

        var context = new WorkflowContext();
        context.Properties["items"] = new object[] { 1, 2, 3, 4, 5 };

        await aggregator.ExecuteAsync(context);

        collectedCount.Should().Be(2);
    }

    [Fact]
    public async Task Aggregator_CompletesOnPredicate()
    {
        var collectedCount = 0;
        var options = new AggregatorOptions().CompleteWhen(items => items.Count >= 3);

        var aggregator = new AggregatorStep(
            ctx => (IEnumerable<object>)ctx.Properties["items"]!,
            (items, ctx) => { collectedCount = items.Count; return Task.CompletedTask; },
            options);

        var context = new WorkflowContext();
        context.Properties["items"] = new object[] { 1, 2, 3, 4, 5 };

        await aggregator.ExecuteAsync(context);

        collectedCount.Should().Be(3);
    }

    #endregion

    #region Scatter-Gather

    [Fact]
    public async Task ScatterGather_AggregatesResultsFromMultipleHandlers()
    {
        var handler1 = new TestStep("H1", ctx =>
        {
            ctx.Properties["__Result_H1"] = "result1";
            return Task.CompletedTask;
        });
        var handler2 = new TestStep("H2", ctx =>
        {
            ctx.Properties["__Result_H2"] = "result2";
            return Task.CompletedTask;
        });

        object?[]? aggregatedResults = null;
        var step = new ScatterGatherStep(
            new[] { handler1, handler2 },
            (results, ctx) => { aggregatedResults = results.ToArray(); return Task.CompletedTask; },
            TimeSpan.FromSeconds(5));

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        aggregatedResults.Should().NotBeNull();
        aggregatedResults.Should().HaveCount(2);
    }

    [Fact]
    public async Task ScatterGather_HandlesTimeoutGracefully()
    {
        var slowHandler = new TestStep("Slow", async ctx =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ctx.CancellationToken);
        });
        var fastHandler = new TestStep("Fast", ctx =>
        {
            ctx.Properties["__Result_Fast"] = "fast_result";
            return Task.CompletedTask;
        });

        var step = new ScatterGatherStep(
            new IStep[] { slowHandler, fastHandler },
            (results, ctx) => Task.CompletedTask,
            TimeSpan.FromMilliseconds(200));

        var context = new WorkflowContext();
        // Should not throw
        await step.ExecuteAsync(context);
    }

    #endregion

    #region Claim Check

    [Fact]
    public async Task ClaimCheck_StoreAndRetrieveCycle()
    {
        var store = new InMemoryClaimCheckStore();
        var largePayload = new { Data = new string('x', 10000) };

        var checkStep = new ClaimCheckStep(store, ctx => ctx.Properties["payload"]!);
        var retrieveStep = new ClaimRetrieveStep(store);

        var context = new WorkflowContext();
        context.Properties["payload"] = largePayload;

        await checkStep.ExecuteAsync(context);

        context.Properties.Should().ContainKey(ClaimCheckStep.ClaimTicketKey);
        var ticket = (string)context.Properties[ClaimCheckStep.ClaimTicketKey]!;
        ticket.Should().NotBeNullOrEmpty();

        await retrieveStep.ExecuteAsync(context);

        context.Properties["__ClaimPayload"].Should().BeSameAs(largePayload);
    }

    #endregion

    #region Dead Letter

    [Fact]
    public async Task DeadLetter_RoutesFailedItemsToStore()
    {
        var deadLetterStore = Substitute.For<IDeadLetterStore>();
        var failingStep = new TestStep("Fail", ctx => throw new InvalidOperationException("Processing failed"));

        var step = new DeadLetterStep(deadLetterStore, failingStep);
        var context = new WorkflowContext();

        await step.ExecuteAsync(context);

        await deadLetterStore.Received(1).SendAsync(
            Arg.Any<object>(),
            Arg.Is<string>(s => s.Contains("Processing failed")),
            Arg.Any<Exception>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Wire Tap

    [Fact]
    public async Task WireTap_DoesNotAffectMainFlow()
    {
        var tapped = false;
        var wireTap = new WireTapStep(ctx =>
        {
            tapped = true;
            return Task.CompletedTask;
        });

        var context = new WorkflowContext();
        await wireTap.ExecuteAsync(context);

        tapped.Should().BeTrue();
        context.IsAborted.Should().BeFalse();
        context.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task WireTap_SwallowsErrors()
    {
        var wireTap = new WireTapStep(ctx => throw new Exception("Tap failed"), swallowErrors: true);

        var context = new WorkflowContext();
        var act = () => wireTap.ExecuteAsync(context);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task WireTap_PropagatesErrorsWhenConfigured()
    {
        var wireTap = new WireTapStep(ctx => throw new Exception("Tap failed"), swallowErrors: false);

        var context = new WorkflowContext();
        var act = () => wireTap.ExecuteAsync(context);

        await act.Should().ThrowAsync<Exception>().WithMessage("Tap failed");
    }

    #endregion

    #region Resequencer

    [Fact]
    public async Task Resequencer_ReordersItems()
    {
        var step = new ResequencerStep(
            ctx => (IEnumerable<object>)ctx.Properties["items"]!,
            item => (long)(int)item);

        var context = new WorkflowContext();
        context.Properties["items"] = new object[] { 3, 1, 4, 1, 5, 9, 2, 6 };

        await step.ExecuteAsync(context);

        var result = (List<object>)context.Properties[ResequencerStep.ResultKey]!;
        result.Select(x => (int)x).Should().BeInAscendingOrder();
    }

    #endregion

    #region Idempotent Receiver

    [Fact]
    public async Task IdempotentReceiver_ProcessesSameMessageOnlyOnce()
    {
        var executionCount = 0;
        var inner = new TestStep("Inner", ctx => { executionCount++; return Task.CompletedTask; });
        var step = new IdempotentReceiverStep(inner, ctx => (string)ctx.Properties["messageId"]!);

        var context = new WorkflowContext();
        context.Properties["messageId"] = "msg-001";

        await step.ExecuteAsync(context);
        await step.ExecuteAsync(context);
        await step.ExecuteAsync(context);

        executionCount.Should().Be(1);
    }

    #endregion

    #region Process Manager

    [Fact]
    public async Task ProcessManager_TransitionsStates()
    {
        var stateLog = new List<string>();

        var handlers = new Dictionary<string, IStep>
        {
            ["initial"] = new TestStep("Init", ctx =>
            {
                stateLog.Add("initial");
                ctx.Properties["state"] = "processing";
                return Task.CompletedTask;
            }),
            ["processing"] = new TestStep("Process", ctx =>
            {
                stateLog.Add("processing");
                ctx.Properties["state"] = "complete";
                return Task.CompletedTask;
            }),
        };

        var step = new ProcessManagerStep(
            ctx => (string)(ctx.Properties.TryGetValue("state", out var s) ? s! : "initial"),
            handlers);

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        stateLog.Should().ContainInOrder("initial", "processing");
        context.Properties["state"].Should().Be("complete");
    }

    #endregion

    #region Normalizer

    [Fact]
    public async Task Normalizer_RoutesToCorrectTranslator()
    {
        var translated = "";
        var jsonTranslator = new TestStep("JSON", ctx => { translated = "json"; return Task.CompletedTask; });
        var xmlTranslator = new TestStep("XML", ctx => { translated = "xml"; return Task.CompletedTask; });

        var step = new NormalizerStep(
            ctx => (string)ctx.Properties["format"]!,
            new Dictionary<string, IStep> { ["json"] = jsonTranslator, ["xml"] = xmlTranslator });

        var context = new WorkflowContext();
        context.Properties["format"] = "xml";

        await step.ExecuteAsync(context);

        translated.Should().Be("xml");
    }

    #endregion

    #region Routing Slip

    [Fact]
    public async Task RoutingSlip_ExecutesStepsInOrder()
    {
        var log = new List<string>();
        var registry = new Dictionary<string, IStep>
        {
            ["validate"] = new TestStep("validate", ctx => { log.Add("validate"); return Task.CompletedTask; }),
            ["enrich"] = new TestStep("enrich", ctx => { log.Add("enrich"); return Task.CompletedTask; }),
            ["publish"] = new TestStep("publish", ctx => { log.Add("publish"); return Task.CompletedTask; }),
        };

        var step = new RoutingSlipStep(
            ctx => new RoutingSlip(new[] { "validate", "enrich", "publish" }),
            registry);

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        log.Should().ContainInOrder("validate", "enrich", "publish");
    }

    #endregion

    #region Fluent Builder

    [Fact]
    public async Task FluentBuilder_IntegrationPatterns()
    {
        var tapped = false;
        var workflow = new WorkflowBuilder()
            .WithName("IntegrationTest")
            .Filter(ctx => ctx.Properties.ContainsKey("proceed"))
            .Enrich(ctx => { ctx.Properties["enriched"] = true; return Task.CompletedTask; })
            .WireTap(ctx => { tapped = true; return Task.CompletedTask; })
            .Build();

        var context = new WorkflowContext();
        context.Properties["proceed"] = true;

        await workflow.ExecuteAsync(context);

        context.Properties["enriched"].Should().Be(true);
        tapped.Should().BeTrue();
    }

    #endregion

    #region Composed Message Processor

    [Fact]
    public async Task ComposedMessageProcessor_SplitsProcessesAggregates()
    {
        var processor = new TestStep("Double", ctx =>
        {
            var item = (int)ctx.Properties[SplitterStep.CurrentItemKey]!;
            ctx.Properties["__ProcessedItem"] = item * 2;
            return Task.CompletedTask;
        });

        object? aggregatedResult = null;
        var step = new ComposedMessageProcessorStep(
            ctx => ((int[])ctx.Properties["numbers"]!).Cast<object>(),
            processor,
            (items, ctx) =>
            {
                aggregatedResult = items.Cast<int>().Sum();
                ctx.Properties[ComposedMessageProcessorStep.ResultKey] = aggregatedResult;
                return Task.CompletedTask;
            });

        var context = new WorkflowContext();
        context.Properties["numbers"] = new[] { 1, 2, 3 };

        await step.ExecuteAsync(context);

        aggregatedResult.Should().Be(12); // (1*2) + (2*2) + (3*2) = 12
    }

    #endregion

    #region Message Translator

    [Fact]
    public async Task MessageTranslator_TransformsData()
    {
        var translator = Substitute.For<IMessageTranslator<string, int>>();
        translator.TranslateAsync("42", Arg.Any<CancellationToken>()).Returns(42);

        var step = new MessageTranslatorStep<string, int>(
            translator,
            ctx => (string)ctx.Properties["input"]!);

        var context = new WorkflowContext();
        context.Properties["input"] = "42";

        await step.ExecuteAsync(context);

        context.Properties["__TranslatedOutput"].Should().Be(42);
    }

    #endregion

    #region Transactional Outbox

    [Fact]
    public async Task TransactionalOutbox_SavesMessage()
    {
        var outbox = Substitute.For<IOutboxStore>();
        outbox.SaveAsync(Arg.Any<object>(), Arg.Any<CancellationToken>()).Returns("msg-123");

        var step = new TransactionalOutboxStep(outbox, ctx => ctx.Properties["message"]!);

        var context = new WorkflowContext();
        context.Properties["message"] = new { OrderId = 1 };

        await step.ExecuteAsync(context);

        context.Properties[TransactionalOutboxStep.OutboxIdKey].Should().Be("msg-123");
        await outbox.Received(1).SaveAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Helpers

    private sealed class TestStep : IStep
    {
        private readonly Func<IWorkflowContext, Task> _action;

        public TestStep(string name, Func<IWorkflowContext, Task> action)
        {
            Name = name;
            _action = action;
        }

        public string Name { get; }
        public Task ExecuteAsync(IWorkflowContext context) => _action(context);
    }

    private sealed class InMemoryClaimCheckStore : IClaimCheckStore
    {
        private readonly Dictionary<string, object> _store = new();

        public Task<string> StoreAsync(object payload, CancellationToken cancellationToken = default)
        {
            var ticket = Guid.NewGuid().ToString("N");
            _store[ticket] = payload;
            return Task.FromResult(ticket);
        }

        public Task<object> RetrieveAsync(string claimTicket, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store[claimTicket]);
        }
    }

    #endregion
}
