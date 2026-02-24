using FluentAssertions;
using WorkflowFramework.Extensions.Connectors.Abstractions;
using WorkflowFramework.Extensions.Connectors.Messaging.Triggers;
using WorkflowFramework.Triggers;
using NSubstitute;
using Xunit;

namespace WorkflowFramework.Tests.Triggers;

public class MessageQueueTriggerSourceTests
{
    [Fact]
    public void Type_IsQueue()
    {
        var connector = Substitute.For<IMessageConnector>();
        new MessageQueueTriggerSource(new TriggerDefinition { Type = "queue" }, connector)
            .Type.Should().Be("queue");
    }

    [Fact]
    public void Constructor_NullConnector_Throws()
    {
        var act = () => new MessageQueueTriggerSource(new TriggerDefinition { Type = "queue" }, null);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_NullDefinition_Throws()
    {
        var connector = Substitute.For<IMessageConnector>();
        var act = () => new MessageQueueTriggerSource(null!, connector);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task StartAsync_MissingSource_Throws()
    {
        var connector = Substitute.For<IMessageConnector>();
        connector.IsConnected.Returns(true);
        var source = new MessageQueueTriggerSource(new TriggerDefinition { Type = "queue" }, connector);
        var ctx = new TriggerContext
        {
            WorkflowId = "wf1",
            Configuration = new Dictionary<string, string>(),
            OnTriggered = _ => Task.FromResult("run1")
        };
        var act = () => source.StartAsync(ctx);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StartAsync_SetsIsRunning()
    {
        var connector = Substitute.For<IMessageConnector>();
        connector.IsConnected.Returns(true);
        var def = new TriggerDefinition
        {
            Type = "queue",
            Configuration = new Dictionary<string, string> { ["source"] = "my-queue" }
        };
        var source = new MessageQueueTriggerSource(def, connector);
        var ctx = new TriggerContext
        {
            WorkflowId = "wf1",
            Configuration = def.Configuration,
            OnTriggered = _ => Task.FromResult("run1")
        };
        await source.StartAsync(ctx);
        source.IsRunning.Should().BeTrue();
        await source.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_ConnectsIfNotConnected()
    {
        var connector = Substitute.For<IMessageConnector>();
        connector.IsConnected.Returns(false);
        var def = new TriggerDefinition
        {
            Type = "queue",
            Configuration = new Dictionary<string, string> { ["source"] = "my-queue" }
        };
        var source = new MessageQueueTriggerSource(def, connector);
        var ctx = new TriggerContext
        {
            WorkflowId = "wf1",
            Configuration = def.Configuration,
            OnTriggered = _ => Task.FromResult("run1")
        };
        await source.StartAsync(ctx);
        await connector.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
        await source.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_SubscribesToSource()
    {
        var connector = Substitute.For<IMessageConnector>();
        connector.IsConnected.Returns(true);
        var def = new TriggerDefinition
        {
            Type = "queue",
            Configuration = new Dictionary<string, string> { ["source"] = "my-queue" }
        };
        var source = new MessageQueueTriggerSource(def, connector);
        var ctx = new TriggerContext
        {
            WorkflowId = "wf1",
            Configuration = def.Configuration,
            OnTriggered = _ => Task.FromResult("run1")
        };
        await source.StartAsync(ctx);
        await connector.Received(1).SubscribeAsync("my-queue", Arg.Any<Func<ConnectorMessage, Task>>(), Arg.Any<CancellationToken>());
        await source.DisposeAsync();
    }

    [Fact]
    public async Task StopAsync_ClearsIsRunning()
    {
        var connector = Substitute.For<IMessageConnector>();
        connector.IsConnected.Returns(true);
        var def = new TriggerDefinition
        {
            Type = "queue",
            Configuration = new Dictionary<string, string> { ["source"] = "q" }
        };
        var source = new MessageQueueTriggerSource(def, connector);
        var ctx = new TriggerContext
        {
            WorkflowId = "wf1",
            Configuration = def.Configuration,
            OnTriggered = _ => Task.FromResult("run1")
        };
        await source.StartAsync(ctx);
        await source.StopAsync();
        source.IsRunning.Should().BeFalse();
        await source.DisposeAsync();
    }
}
