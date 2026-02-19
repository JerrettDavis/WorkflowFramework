using System.Text;
using FluentAssertions;
using WorkflowFramework.Extensions.Connectors.Abstractions;
using WorkflowFramework.Extensions.Connectors.Messaging;
using Xunit;

namespace WorkflowFramework.Tests.Connectors;

public class InMemoryMessageConnectorTests
{
    [Fact]
    public async Task ConnectAsync_SetsIsConnected()
    {
        var connector = new InMemoryMessageConnector("test");
        connector.IsConnected.Should().BeFalse();

        await connector.ConnectAsync();
        connector.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task DisconnectAsync_ClearsIsConnected()
    {
        var connector = new InMemoryMessageConnector("test");
        await connector.ConnectAsync();
        await connector.DisconnectAsync();
        connector.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_ThrowsWhenNotConnected()
    {
        var connector = new InMemoryMessageConnector("test");
        var act = () => connector.SendAsync("queue", Encoding.UTF8.GetBytes("hello"));
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SendAndReceive_RoundTrips()
    {
        var connector = new InMemoryMessageConnector("test");
        await connector.ConnectAsync();

        var payload = Encoding.UTF8.GetBytes("hello world");
        var headers = new Dictionary<string, string> { ["key"] = "value" };

        await connector.SendAsync("queue1", payload, headers);

        var message = await connector.ReceiveAsync("queue1", TimeSpan.FromSeconds(1));

        message.Should().NotBeNull();
        message!.Source.Should().Be("queue1");
        message.Payload.Should().BeEquivalentTo(payload);
        message.Headers.Should().ContainKey("key").WhoseValue.Should().Be("value");
    }

    [Fact]
    public async Task ReceiveAsync_ReturnsNullOnTimeout()
    {
        var connector = new InMemoryMessageConnector("test");
        await connector.ConnectAsync();

        var message = await connector.ReceiveAsync("empty-queue", TimeSpan.FromMilliseconds(50));
        message.Should().BeNull();
    }

    [Fact]
    public async Task SubscribeAsync_ReceivesMessages()
    {
        var connector = new InMemoryMessageConnector("test");
        await connector.ConnectAsync();

        var received = new List<ConnectorMessage>();
        await connector.SubscribeAsync("events", msg =>
        {
            received.Add(msg);
            return Task.CompletedTask;
        });

        await connector.SendAsync("events", Encoding.UTF8.GetBytes("msg1"));
        await connector.SendAsync("events", Encoding.UTF8.GetBytes("msg2"));

        received.Should().HaveCount(2);
        Encoding.UTF8.GetString(received[0].Payload).Should().Be("msg1");
        Encoding.UTF8.GetString(received[1].Payload).Should().Be("msg2");
    }

    [Fact]
    public async Task TestConnectionAsync_ReflectsState()
    {
        var connector = new InMemoryMessageConnector("test");
        (await connector.TestConnectionAsync()).Should().BeFalse();

        await connector.ConnectAsync();
        (await connector.TestConnectionAsync()).Should().BeTrue();

        await connector.DisconnectAsync();
        (await connector.TestConnectionAsync()).Should().BeFalse();
    }

    [Fact]
    public void Name_ReturnsConfiguredName()
    {
        var connector = new InMemoryMessageConnector("my-connector");
        connector.Name.Should().Be("my-connector");
        connector.Type.Should().Be("InMemory");
    }

    [Fact]
    public async Task MultipleQueues_AreIndependent()
    {
        var connector = new InMemoryMessageConnector("test");
        await connector.ConnectAsync();

        await connector.SendAsync("q1", Encoding.UTF8.GetBytes("a"));
        await connector.SendAsync("q2", Encoding.UTF8.GetBytes("b"));

        var msg1 = await connector.ReceiveAsync("q1", TimeSpan.FromSeconds(1));
        var msg2 = await connector.ReceiveAsync("q2", TimeSpan.FromSeconds(1));

        Encoding.UTF8.GetString(msg1!.Payload).Should().Be("a");
        Encoding.UTF8.GetString(msg2!.Payload).Should().Be("b");
    }
}
