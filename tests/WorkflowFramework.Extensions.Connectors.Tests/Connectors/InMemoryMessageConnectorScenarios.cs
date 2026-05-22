using FluentAssertions;
using System.Text;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Connectors.Abstractions;
using WorkflowFramework.Extensions.Connectors.Messaging;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Connectors.Tests.Connectors;

[Feature("InMemoryMessageConnector — testable in-memory message connector")]
public class InMemoryMessageConnectorScenarios : TinyBddXunitBase
{
    public InMemoryMessageConnectorScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("Name defaults to 'in-memory'"), Fact]
    public async Task Name_DefaultsToInMemory()
    {
        var connector = new InMemoryMessageConnector();

        await Given("InMemoryMessageConnector with default name", () => connector.Name)
            .Then("name is 'in-memory'", name =>
            {
                name.Should().Be("in-memory");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Type is 'InMemory'"), Fact]
    public async Task Type_IsInMemory()
    {
        var connector = new InMemoryMessageConnector();

        await Given("a default InMemoryMessageConnector", () => connector.Type)
            .Then("type is 'InMemory'", t =>
            {
                t.Should().Be("InMemory");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ConnectAsync sets IsConnected to true"), Fact]
    public async Task ConnectAsync_SetsIsConnectedTrue()
    {
        var connector = new InMemoryMessageConnector();
        await connector.ConnectAsync();

        await Given("ConnectAsync called", () => connector.IsConnected)
            .Then("IsConnected is true", connected =>
            {
                connected.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("DisconnectAsync sets IsConnected to false"), Fact]
    public async Task DisconnectAsync_SetsIsConnectedFalse()
    {
        var connector = new InMemoryMessageConnector();
        await connector.ConnectAsync();
        await connector.DisconnectAsync();

        await Given("DisconnectAsync called after Connect", () => connector.IsConnected)
            .Then("IsConnected is false", connected =>
            {
                connected.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("TestConnectionAsync returns true when connected"), Fact]
    public async Task TestConnection_WhenConnected_ReturnsTrue()
    {
        var connector = new InMemoryMessageConnector();
        await connector.ConnectAsync();
        var result = await connector.TestConnectionAsync();

        await Given("connector connected", () => result)
            .Then("TestConnectionAsync returns true", r =>
            {
                r.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("SendAsync throws when not connected"), Fact]
    public async Task SendAsync_WhenNotConnected_Throws()
    {
        var connector = new InMemoryMessageConnector();
        Exception? caught = null;
        try { await connector.SendAsync("dest", Array.Empty<byte>()); }
        catch (Exception ex) { caught = ex; }

        await Given("connector not connected", () => caught)
            .Then("InvalidOperationException is thrown", ex =>
            {
                ex.Should().BeOfType<InvalidOperationException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ReceiveAsync dequeues sent message"), Fact]
    public async Task ReceiveAsync_DequeuesSentMessage()
    {
        var connector = new InMemoryMessageConnector();
        await connector.ConnectAsync();
        var payload = Encoding.UTF8.GetBytes("hello");
        await connector.SendAsync("queue", payload);

        var received = await connector.ReceiveAsync("queue", TimeSpan.FromSeconds(1));

        await Given("message sent and then received from same queue", () => received)
            .Then("received message has correct payload", msg =>
            {
                msg.Should().NotBeNull();
                msg!.Payload.Should().Equal(payload);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("SubscribeAsync handler is invoked on send"), Fact]
    public async Task SubscribeAsync_HandlerInvokedOnSend()
    {
        var connector = new InMemoryMessageConnector();
        await connector.ConnectAsync();
        ConnectorMessage? received = null;
        await connector.SubscribeAsync("topic", msg => { received = msg; return Task.CompletedTask; });

        await connector.SendAsync("topic", Encoding.UTF8.GetBytes("event"));

        await Given("subscriber registered before SendAsync", () => received)
            .Then("subscriber handler received the message", msg =>
            {
                msg.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Custom name is reflected in Name property"), Fact]
    public async Task CustomName_ReflectedInProperty()
    {
        var connector = new InMemoryMessageConnector("my-bus");

        await Given("connector created with name 'my-bus'", () => connector.Name)
            .Then("Name is 'my-bus'", name =>
            {
                name.Should().Be("my-bus");
                return true;
            })
            .AssertPassed();
    }
}
