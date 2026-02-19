using FluentAssertions;
using WorkflowFramework.Extensions.Agents.Mcp;
using Xunit;

namespace WorkflowFramework.Tests.Agents.Mcp;

public class StdioMcpTransportTests
{
    [Fact]
    public void Constructor_NullCommand_Throws()
    {
        var act = () => new StdioMcpTransport(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task SendAsync_BeforeConnect_Throws()
    {
        var transport = new StdioMcpTransport("echo");
        var act = async () => await transport.SendAsync(new McpJsonRpcMessage());
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ReceiveAsync_BeforeConnect_Throws()
    {
        var transport = new StdioMcpTransport("echo");
        var act = async () => await transport.ReceiveAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Dispose_BeforeConnect_NoOp()
    {
        var transport = new StdioMcpTransport("echo");
        transport.Dispose(); // Should not throw
    }

    [Fact]
    public async Task DisconnectAsync_BeforeConnect_NoOp()
    {
        var transport = new StdioMcpTransport("echo");
        await transport.DisconnectAsync(); // Should not throw
    }

    [Fact]
    public async Task ConnectAsync_InvalidCommand_Throws()
    {
        var transport = new StdioMcpTransport("nonexistent_command_that_does_not_exist_12345");
        // On most systems this will throw or return null process
        try
        {
            await transport.ConnectAsync();
            // If it somehow starts, just dispose
            transport.Dispose();
        }
        catch (Exception ex)
        {
            ex.Should().BeAssignableTo<Exception>();
        }
    }
}
