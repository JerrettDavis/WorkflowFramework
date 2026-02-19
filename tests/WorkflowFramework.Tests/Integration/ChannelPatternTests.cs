using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Integration.Abstractions;
using WorkflowFramework.Extensions.Integration.Channel;
using Xunit;

namespace WorkflowFramework.Tests.Integration;

public class ChannelPatternTests
{
    #region ChannelAdapter

    [Fact]
    public void ChannelAdapter_NullAdapter_Throws()
    {
        var act = () => new ChannelAdapterStep(null!, ctx => "msg");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ChannelAdapter_NullMessageSelector_Throws()
    {
        var act = () => new ChannelAdapterStep(Substitute.For<IChannelAdapter>(), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ChannelAdapter_SendsMessage()
    {
        var adapter = Substitute.For<IChannelAdapter>();
        var step = new ChannelAdapterStep(adapter, ctx => ctx.Properties["msg"]!);
        var context = new WorkflowContext();
        context.Properties["msg"] = "hello";
        await step.ExecuteAsync(context);
        await adapter.Received(1).SendAsync("hello", Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ChannelAdapter_Name() => new ChannelAdapterStep(Substitute.For<IChannelAdapter>(), ctx => "x").Name.Should().Be("ChannelAdapter");

    #endregion

    #region MessageBridge

    [Fact]
    public void MessageBridge_NullSource_Throws()
    {
        var act = () => new MessageBridgeStep(null!, Substitute.For<IChannelAdapter>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void MessageBridge_NullDest_Throws()
    {
        var act = () => new MessageBridgeStep(Substitute.For<IChannelAdapter>(), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task MessageBridge_BridgesMessage()
    {
        var source = Substitute.For<IChannelAdapter>();
        var dest = Substitute.For<IChannelAdapter>();
        source.ReceiveAsync(Arg.Any<CancellationToken>()).Returns("bridged");
        var step = new MessageBridgeStep(source, dest);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        await dest.Received(1).SendAsync("bridged", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MessageBridge_NullMessage_DoesNotSend()
    {
        var source = Substitute.For<IChannelAdapter>();
        var dest = Substitute.For<IChannelAdapter>();
        source.ReceiveAsync(Arg.Any<CancellationToken>()).Returns((object?)null);
        var step = new MessageBridgeStep(source, dest);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        await dest.DidNotReceive().SendAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void MessageBridge_Name() => new MessageBridgeStep(Substitute.For<IChannelAdapter>(), Substitute.For<IChannelAdapter>()).Name.Should().Be("MessageBridge");

    #endregion

    #region WireTap

    [Fact]
    public void WireTap_NullAction_Throws()
    {
        var act = () => new WireTapStep(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task WireTap_NonDisruptive()
    {
        var tapped = false;
        var step = new WireTapStep(ctx => { tapped = true; return Task.CompletedTask; });
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        tapped.Should().BeTrue();
        context.IsAborted.Should().BeFalse();
    }

    [Fact]
    public async Task WireTap_SwallowsErrors_ByDefault()
    {
        var step = new WireTapStep(ctx => throw new Exception("tap error"));
        var context = new WorkflowContext();
        await step.ExecuteAsync(context); // Should not throw
    }

    [Fact]
    public async Task WireTap_PropagatesErrors_WhenConfigured()
    {
        var step = new WireTapStep(ctx => throw new Exception("tap error"), swallowErrors: false);
        var context = new WorkflowContext();
        var act = () => step.ExecuteAsync(context);
        await act.Should().ThrowAsync<Exception>().WithMessage("tap error");
    }

    [Fact]
    public void WireTap_Name() => new WireTapStep(ctx => Task.CompletedTask).Name.Should().Be("WireTap");

    #endregion

    #region DeadLetter

    [Fact]
    public void DeadLetter_NullStore_Throws()
    {
        var act = () => new DeadLetterStep(null!, new TestStep("inner"));
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void DeadLetter_NullInnerStep_Throws()
    {
        var act = () => new DeadLetterStep(Substitute.For<IDeadLetterStore>(), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task DeadLetter_SuccessfulStep_DoesNotRoute()
    {
        var store = Substitute.For<IDeadLetterStore>();
        var inner = new TestStep("inner");
        var step = new DeadLetterStep(store, inner);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        await store.DidNotReceive().SendAsync(Arg.Any<object>(), Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeadLetter_FailingStep_RoutesToStore()
    {
        var store = Substitute.For<IDeadLetterStore>();
        var inner = new TestStep("fail", ctx => throw new InvalidOperationException("process error"));
        var step = new DeadLetterStep(store, inner);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);
        await store.Received(1).SendAsync(Arg.Any<object>(), Arg.Is<string>(s => s.Contains("process error")), Arg.Any<Exception>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeadLetter_UsesCurrentMessage_IfAvailable()
    {
        var store = Substitute.For<IDeadLetterStore>();
        var inner = new TestStep("fail", ctx => throw new Exception("err"));
        var step = new DeadLetterStep(store, inner);
        var context = new WorkflowContext();
        context.Properties["__CurrentMessage"] = "the-message";
        await step.ExecuteAsync(context);
        await store.Received(1).SendAsync("the-message", Arg.Any<string>(), Arg.Any<Exception>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void DeadLetter_Name() => new DeadLetterStep(Substitute.For<IDeadLetterStore>(), new TestStep("inner")).Name.Should().Be("DeadLetter");

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
