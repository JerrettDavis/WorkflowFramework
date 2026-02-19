using FluentAssertions;
using WorkflowFramework.Extensions.Connectors.Grpc;
using Xunit;

namespace WorkflowFramework.Tests.Connectors;

public class GrpcConnectorTests
{
    [Fact]
    public void GrpcConnectorConfig_DefaultValues()
    {
        var config = new GrpcConnectorConfig();
        config.Address.Should().BeEmpty();
        config.UseTls.Should().BeTrue();
        config.Deadline.Should().Be(TimeSpan.FromSeconds(30));
        config.MaxMessageSize.Should().Be(4 * 1024 * 1024);
    }

    [Fact]
    public void GrpcConnectorConfig_SetProperties()
    {
        var config = new GrpcConnectorConfig
        {
            Address = "https://localhost:5001",
            UseTls = false,
            Deadline = TimeSpan.FromSeconds(60),
            MaxMessageSize = 1024
        };
        config.Address.Should().Be("https://localhost:5001");
        config.UseTls.Should().BeFalse();
        config.Deadline.Should().Be(TimeSpan.FromSeconds(60));
        config.MaxMessageSize.Should().Be(1024);
    }

    [Fact]
    public void GrpcConnectorConfig_InheritsConnectorConfiguration()
    {
        var config = new GrpcConnectorConfig { Name = "grpc1", Type = "Grpc" };
        config.Name.Should().Be("grpc1");
        config.Type.Should().Be("Grpc");
    }
}
