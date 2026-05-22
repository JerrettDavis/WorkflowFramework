using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Integration.Endpoint;
using WorkflowFramework.Extensions.Integration.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Integration.Endpoint;

// Bespoke kept: TransactionalOutboxStep is a persistence-boundary EIP primitive that wraps
// IOutboxStore — a domain interface representing atomic write semantics with a backing store.
// PatternKit has no "outbox" or "transactional write" primitive. Characterization-only
// coverage locks the current contract.

[Feature("TransactionalOutboxStep — characterization (Phase G.4)")]
public class TransactionalOutboxStepScenarios : TinyBddTestBase
{
    public TransactionalOutboxStepScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("TransactionalOutboxStep.Name returns 'TransactionalOutbox'"), Fact]
    public async Task NameIsTransactionalOutbox()
    {
        var store = Substitute.For<IOutboxStore>();
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
        var store = Substitute.For<IOutboxStore>();
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

    [Scenario("ExecuteAsync saves the selected message to the outbox store"), Fact]
    public async Task ExecuteSavesMessageToStore()
    {
        var savedMessage = default(object?);
        var store = Substitute.For<IOutboxStore>();
        store.SaveAsync(Arg.Do<object>(m => savedMessage = m), Arg.Any<CancellationToken>())
             .Returns("outbox-id-42");

        var payload = new { Text = "hello" };
        var ctx = new WorkflowContext();
        ctx.Properties["payload"] = payload;
        var sut = new TransactionalOutboxStep(store, c => c.Properties["payload"]!);
        await sut.ExecuteAsync(ctx);

        await Given("message saved to outbox store", () => savedMessage)
            .Then("saved message is the one returned by the selector", m =>
            {
                m.Should().BeSameAs(payload);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Returned outbox ID is stored on context under OutboxIdKey"), Fact]
    public async Task OutboxIdStoredOnContext()
    {
        var store = Substitute.For<IOutboxStore>();
        store.SaveAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
             .Returns("msg-abc-123");

        var ctx = new WorkflowContext();
        var sut = new TransactionalOutboxStep(store, _ => "any-message");
        await sut.ExecuteAsync(ctx);

        await Given("context after outbox step executes", () => ctx)
            .Then("OutboxIdKey property holds the ID returned by the store", c =>
            {
                c.Properties[TransactionalOutboxStep.OutboxIdKey].Should().Be("msg-abc-123");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("SaveAsync is called with the context cancellation token"), Fact]
    public async Task SaveAsyncReceivesContextCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var capturedToken = CancellationToken.None;

        var store = Substitute.For<IOutboxStore>();
        store.SaveAsync(
                Arg.Any<object>(),
                Arg.Do<CancellationToken>(t => capturedToken = t))
             .Returns("id");

        var ctx = new WorkflowContext(cts.Token);
        var sut = new TransactionalOutboxStep(store, _ => "msg");
        await sut.ExecuteAsync(ctx);

        await Given("captured CancellationToken passed to SaveAsync", () => capturedToken)
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
        var store = Substitute.For<IOutboxStore>();
        store.SaveAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
             .Returns<Task<string>>(_ => throw new InvalidOperationException("store unavailable"));

        Exception? caught = null;
        var sut = new TransactionalOutboxStep(store, _ => "msg");
        try { await sut.ExecuteAsync(new WorkflowContext()); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("exception from SaveAsync", () => caught)
            .Then("InvalidOperationException propagates to caller", ex =>
            {
                ex.Should().NotBeNull();
                ex!.Message.Should().Be("store unavailable");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Each ExecuteAsync call invokes SaveAsync once"), Fact]
    public async Task EachExecutionCallsSaveAsyncOnce()
    {
        var callCount = 0;
        var store = Substitute.For<IOutboxStore>();
        store.SaveAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
             .Returns(_ => { callCount++; return Task.FromResult($"id-{callCount}"); });

        var sut = new TransactionalOutboxStep(store, _ => "msg");
        var ctx = new WorkflowContext();
        await sut.ExecuteAsync(ctx);
        await sut.ExecuteAsync(ctx);

        await Given("call count after two executions", () => callCount)
            .Then("SaveAsync was called twice", count =>
            {
                count.Should().Be(2);
                return true;
            })
            .AssertPassed();
    }
}
