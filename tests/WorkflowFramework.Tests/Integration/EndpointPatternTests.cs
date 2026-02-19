using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Integration.Abstractions;
using WorkflowFramework.Extensions.Integration.Endpoint;
using Xunit;

namespace WorkflowFramework.Tests.Integration;

public class EndpointPatternTests
{
    #region PollingConsumer

    [Fact]
    public void PollingConsumer_NullSource_Throws()
    {
        var act = () => new PollingConsumerStep<string>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task PollingConsumer_StoresPolledItems()
    {
        var source = Substitute.For<IPollingSource<string>>();
        source.PollAsync(Arg.Any<CancellationToken>()).Returns(new List<string> { "a", "b" });
        var step = new PollingConsumerStep<string>(source);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        context.Properties[PollingConsumerStep<string>.ResultKey].Should().BeEquivalentTo(new[] { "a", "b" });
    }

    [Fact]
    public async Task PollingConsumer_EmptySource_StoresEmptyList()
    {
        var source = Substitute.For<IPollingSource<int>>();
        source.PollAsync(Arg.Any<CancellationToken>()).Returns(new List<int>());
        var step = new PollingConsumerStep<int>(source);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        var items = context.Properties[PollingConsumerStep<int>.ResultKey] as IReadOnlyList<int>;
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task PollingConsumer_SourceError_Propagates()
    {
        var source = Substitute.For<IPollingSource<string>>();
        source.PollAsync(Arg.Any<CancellationToken>()).Returns<IReadOnlyList<string>>(x => throw new Exception("poll error"));
        var step = new PollingConsumerStep<string>(source);
        var context = new WorkflowContext();
        var act = () => step.ExecuteAsync(context);
        await act.Should().ThrowAsync<Exception>().WithMessage("poll error");
    }

    [Fact]
    public void PollingConsumer_Name() => new PollingConsumerStep<string>(Substitute.For<IPollingSource<string>>()).Name.Should().Be("PollingConsumer");

    #endregion

    #region IdempotentReceiver

    [Fact]
    public void IdempotentReceiver_NullInnerStep_Throws()
    {
        var act = () => new IdempotentReceiverStep(null!, ctx => "id");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IdempotentReceiver_NullIdSelector_Throws()
    {
        var act = () => new IdempotentReceiverStep(new TestStep("inner"), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task IdempotentReceiver_DuplicateRejection()
    {
        var count = 0;
        var inner = new TestStep("inner", ctx => { count++; return Task.CompletedTask; });
        var step = new IdempotentReceiverStep(inner, ctx => "msg-1");
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        await step.ExecuteAsync(context);
        await step.ExecuteAsync(context);
        count.Should().Be(1);
    }

    [Fact]
    public async Task IdempotentReceiver_DifferentIds_AllProcessed()
    {
        var count = 0;
        var inner = new TestStep("inner", ctx => { count++; return Task.CompletedTask; });
        var step = new IdempotentReceiverStep(inner, ctx => (string)ctx.Properties["id"]!);
        var context1 = new WorkflowContext();
        context1.Properties["id"] = "a";
        var context2 = new WorkflowContext();
        context2.Properties["id"] = "b";
        await step.ExecuteAsync(context1);
        await step.ExecuteAsync(context2);
        count.Should().Be(2);
    }

    [Fact]
    public async Task IdempotentReceiver_ThreadSafety()
    {
        var count = 0;
        var inner = new TestStep("inner", ctx => { Interlocked.Increment(ref count); return Task.CompletedTask; });
        var step = new IdempotentReceiverStep(inner, ctx => "same-id");
        var tasks = Enumerable.Range(0, 10).Select(_ =>
        {
            var ctx = new WorkflowContext();
            return step.ExecuteAsync(ctx);
        });
        await Task.WhenAll(tasks);
        count.Should().Be(1);
    }

    [Fact]
    public void IdempotentReceiver_Name() => new IdempotentReceiverStep(new TestStep("inner"), ctx => "id").Name.Should().Be("IdempotentReceiver");

    #endregion

    #region TransactionalOutbox

    [Fact]
    public void TransactionalOutbox_NullStore_Throws()
    {
        var act = () => new TransactionalOutboxStep(null!, ctx => "msg");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TransactionalOutbox_NullSelector_Throws()
    {
        var act = () => new TransactionalOutboxStep(Substitute.For<IOutboxStore>(), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task TransactionalOutbox_SavesAndStoresId()
    {
        var outbox = Substitute.For<IOutboxStore>();
        outbox.SaveAsync(Arg.Any<object>(), Arg.Any<CancellationToken>()).Returns("outbox-123");
        var step = new TransactionalOutboxStep(outbox, ctx => ctx.Properties["msg"]!);
        var context = new WorkflowContext();
        context.Properties["msg"] = "payload";
        await step.ExecuteAsync(context);
        context.Properties[TransactionalOutboxStep.OutboxIdKey].Should().Be("outbox-123");
        await outbox.Received(1).SaveAsync("payload", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TransactionalOutbox_Name() => new TransactionalOutboxStep(Substitute.For<IOutboxStore>(), ctx => "x").Name.Should().Be("TransactionalOutbox");

    #endregion

    #region OutboxMessage Model

    [Fact]
    public void OutboxMessage_DefaultValues()
    {
        var msg = new OutboxMessage();
        msg.Id.Should().BeEmpty();
        msg.Payload.Should().BeNull();
        msg.IsSent.Should().BeFalse();
    }

    [Fact]
    public void OutboxMessage_SetProperties()
    {
        var now = DateTimeOffset.UtcNow;
        var msg = new OutboxMessage { Id = "x", Payload = "data", CreatedAt = now, IsSent = true };
        msg.Id.Should().Be("x");
        msg.Payload.Should().Be("data");
        msg.CreatedAt.Should().Be(now);
        msg.IsSent.Should().BeTrue();
    }

    #endregion

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
