using Xunit;
using FluentAssertions;
using WorkflowFramework.Extensions.Connectors.Abstractions;
using WorkflowFramework.Extensions.Connectors.Messaging.Abstractions;

namespace WorkflowFramework.Tests.DataMapping;

public class ConnectorRegistryTests
{
    [Fact]
    public void Register_And_Get()
    {
        var registry = new ConnectorRegistry();
        var connector = new MockConnector("test", "Mock");
        registry.Register(connector);
        registry.Get("test").Should().BeSameAs(connector);
    }

    [Fact]
    public void Register_Duplicate_Throws()
    {
        var registry = new ConnectorRegistry();
        registry.Register(new MockConnector("test", "Mock"));
        var act = () => registry.Register(new MockConnector("test", "Mock"));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Get_Unknown_ReturnsNull()
    {
        var registry = new ConnectorRegistry();
        registry.Get("missing").Should().BeNull();
    }

    [Fact]
    public void Names_ReturnsAll()
    {
        var registry = new ConnectorRegistry();
        registry.Register(new MockConnector("a", "Mock"));
        registry.Register(new MockConnector("b", "Mock"));
        registry.Names.Should().Contain("a").And.Contain("b");
    }
}

public class BrokerMessageTests
{
    [Fact]
    public void DefaultValues_AreSet()
    {
        var msg = new BrokerMessage();
        msg.Id.Should().NotBeNullOrEmpty();
        msg.ContentType.Should().Be("application/json");
        msg.Body.Should().BeEmpty();
    }
}

internal class MockConnector : IConnector
{
    public MockConnector(string name, string type)
    {
        Name = name;
        Type = type;
    }

    public string Name { get; }
    public string Type { get; }
    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}
