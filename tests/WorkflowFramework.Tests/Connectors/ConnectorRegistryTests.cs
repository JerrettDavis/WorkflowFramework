using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Connectors.Abstractions;
using WorkflowFramework.Extensions.Connectors.Abstractions.BuiltIn;
using Xunit;

namespace WorkflowFramework.Tests.Connectors;

public class ConnectorRegistryTests
{
    [Fact]
    public void Register_NullConnector_Throws()
    {
        var registry = new ConnectorRegistry();
        var act = () => registry.Register(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Register_AndGet_ReturnsConnector()
    {
        var registry = new ConnectorRegistry();
        var connector = CreateConnector("test");
        registry.Register(connector);
        registry.Get("test").Should().BeSameAs(connector);
    }

    [Fact]
    public void Register_Duplicate_Throws()
    {
        var registry = new ConnectorRegistry();
        registry.Register(CreateConnector("test"));
        var act = () => registry.Register(CreateConnector("test"));
        act.Should().Throw<InvalidOperationException>().WithMessage("*test*");
    }

    [Fact]
    public void Get_NonExistent_ReturnsNull()
    {
        var registry = new ConnectorRegistry();
        registry.Get("missing").Should().BeNull();
    }

    [Fact]
    public void Get_CaseInsensitive()
    {
        var registry = new ConnectorRegistry();
        var connector = CreateConnector("MyConnector");
        registry.Register(connector);
        registry.Get("myconnector").Should().BeSameAs(connector);
    }

    [Fact]
    public void Names_ReturnsAllRegistered()
    {
        var registry = new ConnectorRegistry();
        registry.Register(CreateConnector("a"));
        registry.Register(CreateConnector("b"));
        registry.Names.Should().Contain("a").And.Contain("b");
    }

    [Fact]
    public void Names_Empty_Initially()
    {
        var registry = new ConnectorRegistry();
        registry.Names.Should().BeEmpty();
    }

    private static IConnector CreateConnector(string name)
    {
        var connector = Substitute.For<IConnector>();
        connector.Name.Returns(name);
        connector.Type.Returns("Test");
        return connector;
    }
}

public class ConnectorConfigTests
{
    [Fact]
    public void ConnectorConfiguration_DefaultValues()
    {
        var config = new ConnectorConfiguration();
        config.Name.Should().BeEmpty();
        config.Type.Should().BeEmpty();
        config.ConnectionString.Should().BeNull();
        config.Authentication.Should().BeNull();
        config.Retry.Should().NotBeNull();
        config.Properties.Should().BeEmpty();
    }

    [Fact]
    public void RetryConfig_Defaults()
    {
        var retry = new RetryConfig();
        retry.MaxAttempts.Should().Be(3);
        retry.InitialDelay.Should().Be(TimeSpan.FromSeconds(1));
        retry.ExponentialBackoff.Should().BeTrue();
    }

    [Fact]
    public void AuthenticationConfig_Defaults()
    {
        var auth = new AuthenticationConfig();
        auth.Type.Should().BeEmpty();
        auth.Credentials.Should().BeEmpty();
    }

    [Fact]
    public void FileConnectorConfig_Defaults()
    {
        var config = new FileConnectorConfig();
        config.FilePath.Should().BeEmpty();
        config.Encoding.Should().Be("utf-8");
    }

    [Fact]
    public void HttpConnectorConfig_Defaults()
    {
        var config = new HttpConnectorConfig();
        config.BaseUrl.Should().BeEmpty();
        config.Method.Should().Be("GET");
        config.Headers.Should().BeEmpty();
        config.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void SqlConnectorConfig_Defaults()
    {
        var config = new SqlConnectorConfig();
        config.Command.Should().BeNull();
        config.CommandTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void FtpConnectorConfig_Defaults()
    {
        var config = new FtpConnectorConfig();
        config.Host.Should().BeEmpty();
        config.Port.Should().Be(21);
        config.RemotePath.Should().Be("/");
        config.UseSftp.Should().BeFalse();
    }
}
