using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.Agents.Mcp;
using Xunit;

namespace WorkflowFramework.Tests.Agents.Mcp;

public class McpServiceCollectionResolutionTests
{
    [Fact]
    public void AddMcpServer_StdioTransport_ResolvesToolAndContextServices()
    {
        var services = new ServiceCollection();
        services.AddMcpServer(new McpServerConfig
        {
            Name = "stdio-server",
            Transport = "stdio",
            Command = "echo"
        });

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IToolProvider>().Should().BeOfType<McpToolProvider>();
        provider.GetRequiredService<IContextSource>().Should().BeOfType<McpResourceProvider>();
    }

    [Fact]
    public void AddMcpServer_HttpTransport_ResolvesToolAndContextServices()
    {
        var services = new ServiceCollection();
        services.AddMcpServer(new McpServerConfig
        {
            Name = "http-server",
            Transport = "HTTP",
            Url = "https://example.test/mcp"
        });

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IToolProvider>().Should().BeOfType<McpToolProvider>();
        provider.GetRequiredService<IContextSource>().Should().BeOfType<McpResourceProvider>();
    }
}
