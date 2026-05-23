using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Endpoint;
using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Endpoint;

// Phase 3 — re-rooted on PatternKit IOutboxStore<object>.
//
// Behavioral change rationale:
//   - TransactionalOutboxStep now consumes IOutboxStore<object> (PatternKit 0.113.0) instead of
//     the bespoke WF IOutboxStore. The internal call changes from SaveAsync(object) → string
//     to EnqueueObjectAsync(object, headers, ct) → OutboxMessage<object>.
//   - The OutboxIdKey is now sourced from record.Id (same value, previously from SaveAsync return).
//   - The legacy WF IOutboxStore interface is now [Obsolete]; steps consume the PatternKit typed
//     interface directly. A LegacyOutboxStoreAdapter bridges old impls for one release.
//   - Store exception propagation, Name constant, and OutboxIdKey constant are unchanged.
// See .plan/patternkit-iteration-2.md §7.

[Feature("TransactionalOutboxStep — characterization (Phase G.4, updated Phase 3)")]
public class TransactionalOutboxStepScenarios : TinyBddTestBase
{
    public TransactionalOutboxStepScenarios(ITestOutputHelper output) : base(output) { }

    private static OutboxMessage<object> MakeRecord(string id, object payload)
    {
        var msg = new Message<object>(payload, MessageHeaders.Empty);
        return new OutboxMessage<object>(id, msg, DateTimeOffset.UtcNow);
    }

    [Scenario("TransactionalOutboxStep.Name returns 'TransactionalOutbox'"), Fact]
    public async Task NameIsTransactionalOutbox()
    {
        var store = Substitute.For<IOutboxStore<object>>();
        var sut = new TransactionalOutboxStep(store, _ => new object());

        await Given("TransactionalOutboxStep instance", () => sut)
            .Then("Name is 'TransactionalOutbox'", s =>
            {
                s.Name.Should().Be("TransactionalOutbox");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null outboxStore throws ArgumentNullException"), Fact]
    public async Task NullStoreThrows()
    {
        Exception? caught = null;
        try { _ = new TransactionalOutboxStep(null!, _ => new object()); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null outboxStore", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null messageSelector throws ArgumentNullException"), Fact]
    public async Task NullMessageSelectorThrows()
    {
        var store = Substitute.For<IOutboxStore<object>>();
        Exception? caught = null;
        try { _ = new TransactionalOutboxStep(store, null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("construction with null messageSelector", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("OutboxIdKey constant has expected value"), Fact]
    public async Task OutboxIdKeyHasExpectedValue()
    {
        await Given("TransactionalOutboxStep.OutboxIdKey constant", () => TransactionalOutboxStep.OutboxIdKey)
            .Then("value is '__OutboxMessageId'", key =>
            {
                key.Should().Be("__OutboxMessageId");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ExecuteAsync enqueues the selected message payload to the outbox store"), Fact]
    public async Task ExecuteEnqueuesMessageToStore()
    {
        // Behavioral change (Phase 3): internally uses EnqueueObjectAsync (PatternKit extension)
        // which wraps the payload in Message<object> before calling EnqueueAsync.
        // We capture the enqueued message via EnqueueAsync on the substitute.
        object? enqueuedPayload = null;
        var store = Substitute.For<IOutboxStore<object>>();
        store.EnqueueAsync(
                Arg.Do<Message<object>>(m => enqueuedPayload = m.Payload),
                Arg.Any<string?>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<CancellationToken>())
             .Returns(ci => new ValueTask<OutboxMessage<object>>(MakeRecord("outbox-id-42", enqueuedPayload!)));

        var payload = new { Text = "hello" };
        var ctx = new WorkflowContext();
        ctx.Properties["payload"] = payload;
        var sut = new TransactionalOutboxStep(store, c => c.Properties["payload"]!);
        await sut.ExecuteAsync(ctx);

        await Given("payload enqueued to outbox store", () => enqueuedPayload)
            .Then("enqueued payload is the one returned by the selector", m =>
            {
                m.Should().BeSameAs(payload);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Returned outbox record ID is stored on context under OutboxIdKey"), Fact]
    public async Task OutboxIdStoredOnContext()
    {
        // Behavioral change (Phase 3): OutboxIdKey is sourced from record.Id (PatternKit OutboxMessage<T>)
        // rather than from the SaveAsync return string. Same value; different code path.
        var store = Substitute.For<IOutboxStore<object>>();
        store.EnqueueAsync(
                Arg.Any<Message<object>>(),
                Arg.Any<string?>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<CancellationToken>())
             .Returns(new ValueTask<OutboxMessage<object>>(MakeRecord("msg-abc-123", "any")));

        var ctx = new WorkflowContext();
        var sut = new TransactionalOutboxStep(store, _ => "any-message");
        await sut.ExecuteAsync(ctx);

        await Given("context after outbox step executes", () => ctx)
            .Then("OutboxIdKey property holds the ID from the enqueued record", c =>
            {
                c.Properties[TransactionalOutboxStep.OutboxIdKey].Should().Be("msg-abc-123");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("EnqueueAsync is called with the context cancellation token"), Fact]
    public async Task EnqueueAsyncReceivesContextCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var capturedToken = CancellationToken.None;

        var store = Substitute.For<IOutboxStore<object>>();
        store.EnqueueAsync(
                Arg.Any<Message<object>>(),
                Arg.Any<string?>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Do<CancellationToken>(t => capturedToken = t))
             .Returns(new ValueTask<OutboxMessage<object>>(MakeRecord("id", "msg")));

        var ctx = new WorkflowContext(cts.Token);
        var sut = new TransactionalOutboxStep(store, _ => "msg");
        await sut.ExecuteAsync(ctx);

        await Given("captured CancellationToken passed to EnqueueAsync", () => capturedToken)
            .Then("it equals the context's CancellationToken", token =>
            {
                token.Should().Be(cts.Token);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Store exception propagates to caller"), Fact]
    public async Task StoreExceptionPropagates()
    {
        var store = Substitute.For<IOutboxStore<object>>();
        store.EnqueueAsync(
                Arg.Any<Message<object>>(),
                Arg.Any<string?>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<CancellationToken>())
             .Returns<ValueTask<OutboxMessage<object>>>(_ => throw new InvalidOperationException("store unavailable"));

        Exception? caught = null;
        var sut = new TransactionalOutboxStep(store, _ => "msg");
        try { await sut.ExecuteAsync(new WorkflowContext()); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("exception from EnqueueAsync", () => caught)
            .Then("InvalidOperationException propagates to caller", ex =>
            {
                ex.Should().NotBeNull();
                ex!.Message.Should().Be("store unavailable");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Each ExecuteAsync call invokes EnqueueAsync once"), Fact]
    public async Task EachExecutionCallsEnqueueAsyncOnce()
    {
        var callCount = 0;
        var store = Substitute.For<IOutboxStore<object>>();
        store.EnqueueAsync(
                Arg.Any<Message<object>>(),
                Arg.Any<string?>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<CancellationToken>())
             .Returns(_ =>
             {
                 callCount++;
                 return new ValueTask<OutboxMessage<object>>(MakeRecord($"id-{callCount}", "msg"));
             });

        var sut = new TransactionalOutboxStep(store, _ => "msg");
        var ctx = new WorkflowContext();
        await sut.ExecuteAsync(ctx);
        await sut.ExecuteAsync(ctx);

        await Given("call count after two executions", () => callCount)
            .Then("EnqueueAsync was called twice", count =>
            {
                count.Should().Be(2);
                return true;
            })
            .AssertPassed();
    }
}
