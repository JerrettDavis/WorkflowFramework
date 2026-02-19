using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Agents.Mcp;
using Xunit;

namespace WorkflowFramework.Tests.Agents.Mcp;

public class McpToolProviderTests
{
    [Fact]
    public void Constructor_NullClient_Throws()
    {
        var act = () => new McpToolProvider(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ListToolsAsync_MapsFromMcpTools()
    {
        var transport = Substitute.For<IMcpTransport>();
        var response = new McpJsonRpcMessage
        {
            Id = 1,
            Result = JsonSerializer.SerializeToElement(new
            {
                tools = new object[]
                {
                    new { name = "t1", description = "d1", inputSchema = new { type = "object" } },
                    new { name = "t2", description = "d2" }
                }
            })
        };
        transport.ReceiveAsync(Arg.Any<CancellationToken>()).Returns(response);
        var client = new McpClient(transport, "srv");
        var provider = new McpToolProvider(client);

        var tools = await provider.ListToolsAsync();

        tools.Should().HaveCount(2);
        tools[0].Name.Should().Be("t1");
        tools[0].Description.Should().Be("d1");
        tools[0].ParametersSchema.Should().NotBeNullOrEmpty();
        tools[0].Metadata["source"].Should().Be("mcp:srv");
        tools[1].Name.Should().Be("t2");
    }

    [Fact]
    public async Task InvokeToolAsync_MapsToToolResult()
    {
        var transport = Substitute.For<IMcpTransport>();
        var response = new McpJsonRpcMessage
        {
            Id = 1,
            Result = JsonSerializer.SerializeToElement(new
            {
                content = new[] { new { type = "text", text = "result text" } },
                isError = false
            })
        };
        transport.ReceiveAsync(Arg.Any<CancellationToken>()).Returns(response);
        var client = new McpClient(transport, "srv");
        var provider = new McpToolProvider(client);

        var result = await provider.InvokeToolAsync("t1", "{}");

        result.Content.Should().Be("result text");
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeToolAsync_ErrorResult()
    {
        var transport = Substitute.For<IMcpTransport>();
        var response = new McpJsonRpcMessage
        {
            Id = 1,
            Result = JsonSerializer.SerializeToElement(new
            {
                content = new[] { new { type = "text", text = "error" } },
                isError = true
            })
        };
        transport.ReceiveAsync(Arg.Any<CancellationToken>()).Returns(response);
        var client = new McpClient(transport, "srv");
        var provider = new McpToolProvider(client);

        var result = await provider.InvokeToolAsync("t1", "{}");
        result.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task ListToolsAsync_Empty_ReturnsEmpty()
    {
        var transport = Substitute.For<IMcpTransport>();
        var response = new McpJsonRpcMessage
        {
            Id = 1,
            Result = JsonSerializer.SerializeToElement(new { tools = Array.Empty<object>() })
        };
        transport.ReceiveAsync(Arg.Any<CancellationToken>()).Returns(response);
        var client = new McpClient(transport, "srv");
        var provider = new McpToolProvider(client);

        var tools = await provider.ListToolsAsync();
        tools.Should().BeEmpty();
    }
}
