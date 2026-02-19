using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Agents.Mcp;
using Xunit;

namespace WorkflowFramework.Tests.Agents.Mcp;

public class McpClientTests
{
    private static IMcpTransport CreateMockTransport(params McpJsonRpcMessage[] responses)
    {
        var transport = Substitute.For<IMcpTransport>();
        var queue = new Queue<McpJsonRpcMessage>(responses);
        transport.ReceiveAsync(Arg.Any<CancellationToken>())
            .Returns(_ => queue.Dequeue());
        return transport;
    }

    [Fact]
    public void Constructor_NullTransport_Throws()
    {
        var act = () => new McpClient(null!, "server");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullServerName_Throws()
    {
        var transport = Substitute.For<IMcpTransport>();
        var act = () => new McpClient(transport, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ServerName_ReturnsConfiguredName()
    {
        var transport = Substitute.For<IMcpTransport>();
        var client = new McpClient(transport, "myserver");
        client.ServerName.Should().Be("myserver");
    }

    [Fact]
    public async Task ConnectAsync_SendsInitializeAndInitialized()
    {
        var initResponse = new McpJsonRpcMessage
        {
            Id = 1,
            Result = JsonSerializer.SerializeToElement(new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                serverInfo = new { name = "test", version = "1.0" }
            })
        };
        var transport = CreateMockTransport(initResponse);
        var client = new McpClient(transport, "server");

        await client.ConnectAsync();

        await transport.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
        // Should have sent initialize + initialized notification
        await transport.Received(2).SendAsync(Arg.Any<McpJsonRpcMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConnectAsync_ErrorResponse_Throws()
    {
        var errorResponse = new McpJsonRpcMessage
        {
            Id = 1,
            Error = new McpJsonRpcError { Code = -32600, Message = "bad request" }
        };
        var transport = CreateMockTransport(errorResponse);
        var client = new McpClient(transport, "server");

        var act = async () => await client.ConnectAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*bad request*");
    }

    [Fact]
    public async Task DisconnectAsync_CallsTransport()
    {
        var transport = Substitute.For<IMcpTransport>();
        var client = new McpClient(transport, "server");
        await client.DisconnectAsync();
        await transport.Received(1).DisconnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListToolsAsync_ReturnsTools()
    {
        var response = new McpJsonRpcMessage
        {
            Id = 1,
            Result = JsonSerializer.SerializeToElement(new
            {
                tools = new object[]
                {
                    new { name = "tool1", description = "desc1", inputSchema = new { type = "object" } },
                    new { name = "tool2", description = "desc2" }
                }
            })
        };
        var transport = CreateMockTransport(response);
        var client = new McpClient(transport, "server");

        var tools = await client.ListToolsAsync();

        tools.Should().HaveCount(2);
        tools[0].Name.Should().Be("tool1");
        tools[0].Description.Should().Be("desc1");
        tools[0].InputSchema.Should().NotBeNullOrEmpty();
        tools[1].Name.Should().Be("tool2");
    }

    [Fact]
    public async Task ListToolsAsync_ErrorResponse_Throws()
    {
        var response = new McpJsonRpcMessage
        {
            Id = 1,
            Error = new McpJsonRpcError { Code = -1, Message = "failed" }
        };
        var transport = CreateMockTransport(response);
        var client = new McpClient(transport, "server");

        var act = async () => await client.ListToolsAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*tools/list*");
    }

    [Fact]
    public async Task ListToolsAsync_EmptyResult_ReturnsEmpty()
    {
        var response = new McpJsonRpcMessage
        {
            Id = 1,
            Result = JsonSerializer.SerializeToElement(new { tools = Array.Empty<object>() })
        };
        var transport = CreateMockTransport(response);
        var client = new McpClient(transport, "server");

        var tools = await client.ListToolsAsync();
        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task CallToolAsync_ReturnsResult()
    {
        var response = new McpJsonRpcMessage
        {
            Id = 1,
            Result = JsonSerializer.SerializeToElement(new
            {
                content = new[] { new { type = "text", text = "tool output" } },
                isError = false
            })
        };
        var transport = CreateMockTransport(response);
        var client = new McpClient(transport, "server");

        var result = await client.CallToolAsync("tool1", "{\"x\":1}");

        result.Content.Should().Be("tool output");
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task CallToolAsync_NullName_Throws()
    {
        var transport = Substitute.For<IMcpTransport>();
        var client = new McpClient(transport, "server");
        var act = async () => await client.CallToolAsync(null!, "{}");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CallToolAsync_NullArgs_Throws()
    {
        var transport = Substitute.For<IMcpTransport>();
        var client = new McpClient(transport, "server");
        var act = async () => await client.CallToolAsync("tool", null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CallToolAsync_ErrorResponse_Throws()
    {
        var response = new McpJsonRpcMessage
        {
            Id = 1,
            Error = new McpJsonRpcError { Code = -1, Message = "call failed" }
        };
        var transport = CreateMockTransport(response);
        var client = new McpClient(transport, "server");

        var act = async () => await client.CallToolAsync("tool1", "{}");
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*tools/call*");
    }

    [Fact]
    public async Task CallToolAsync_IsError_True()
    {
        var response = new McpJsonRpcMessage
        {
            Id = 1,
            Result = JsonSerializer.SerializeToElement(new
            {
                content = new[] { new { type = "text", text = "error output" } },
                isError = true
            })
        };
        var transport = CreateMockTransport(response);
        var client = new McpClient(transport, "server");

        var result = await client.CallToolAsync("tool1", "{}");
        result.IsError.Should().BeTrue();
        result.Content.Should().Be("error output");
    }

    [Fact]
    public async Task ListResourcesAsync_ReturnsResources()
    {
        var response = new McpJsonRpcMessage
        {
            Id = 1,
            Result = JsonSerializer.SerializeToElement(new
            {
                resources = new[]
                {
                    new { uri = "file:///a.txt", name = "a.txt", description = "file a", mimeType = "text/plain" }
                }
            })
        };
        var transport = CreateMockTransport(response);
        var client = new McpClient(transport, "server");

        var resources = await client.ListResourcesAsync();
        resources.Should().HaveCount(1);
        resources[0].Uri.Should().Be("file:///a.txt");
        resources[0].Name.Should().Be("a.txt");
        resources[0].Description.Should().Be("file a");
        resources[0].MimeType.Should().Be("text/plain");
    }

    [Fact]
    public async Task ListResourcesAsync_ErrorResponse_Throws()
    {
        var response = new McpJsonRpcMessage
        {
            Id = 1,
            Error = new McpJsonRpcError { Code = -1, Message = "list failed" }
        };
        var transport = CreateMockTransport(response);
        var client = new McpClient(transport, "server");

        var act = async () => await client.ListResourcesAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*resources/list*");
    }

    [Fact]
    public async Task ReadResourceAsync_ReturnsContent()
    {
        var response = new McpJsonRpcMessage
        {
            Id = 1,
            Result = JsonSerializer.SerializeToElement(new
            {
                contents = new[]
                {
                    new { uri = "file:///a.txt", text = "hello world", mimeType = "text/plain" }
                }
            })
        };
        var transport = CreateMockTransport(response);
        var client = new McpClient(transport, "server");

        var content = await client.ReadResourceAsync("file:///a.txt");
        content.Uri.Should().Be("file:///a.txt");
        content.Text.Should().Be("hello world");
        content.MimeType.Should().Be("text/plain");
    }

    [Fact]
    public async Task ReadResourceAsync_NullUri_Throws()
    {
        var transport = Substitute.For<IMcpTransport>();
        var client = new McpClient(transport, "server");
        var act = async () => await client.ReadResourceAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ReadResourceAsync_ErrorResponse_Throws()
    {
        var response = new McpJsonRpcMessage
        {
            Id = 1,
            Error = new McpJsonRpcError { Code = -1, Message = "read failed" }
        };
        var transport = CreateMockTransport(response);
        var client = new McpClient(transport, "server");

        var act = async () => await client.ReadResourceAsync("file:///a.txt");
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*resources/read*");
    }

    [Fact]
    public void Dispose_DisposesTransport()
    {
        var transport = Substitute.For<IMcpTransport>();
        var client = new McpClient(transport, "server");
        client.Dispose();
        transport.Received(1).Dispose();
    }

    [Fact]
    public void Dispose_CalledTwice_OnlyDisposesOnce()
    {
        var transport = Substitute.For<IMcpTransport>();
        var client = new McpClient(transport, "server");
        client.Dispose();
        client.Dispose();
        transport.Received(1).Dispose();
    }
}
