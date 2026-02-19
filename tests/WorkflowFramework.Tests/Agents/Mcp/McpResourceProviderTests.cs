using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.Agents.Mcp;
using Xunit;

namespace WorkflowFramework.Tests.Agents.Mcp;

public class McpResourceProviderTests
{
    [Fact]
    public void Constructor_NullClient_Throws()
    {
        var act = () => new McpResourceProvider(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Name_ContainsServerName()
    {
        var transport = Substitute.For<IMcpTransport>();
        using var client = new McpClient(transport, "test-server");
        var provider = new McpResourceProvider(client);
        provider.Name.Should().Be("mcp:test-server");
    }

    [Fact]
    public async Task GetContextAsync_MapsResourcesToDocuments()
    {
        var transport = Substitute.For<IMcpTransport>();
        var callIndex = 0;

        // Set up transport to return resources/list then resources/read responses
        transport.ReceiveAsync(Arg.Any<CancellationToken>()).Returns(callInfo =>
        {
            callIndex++;
            if (callIndex == 1) // resources/list response
            {
                return new McpJsonRpcMessage
                {
                    Id = 1,
                    Result = JsonSerializer.SerializeToElement(new
                    {
                        resources = new[]
                        {
                            new { uri = "file:///doc.md", name = "doc", description = "A document", mimeType = "text/markdown" }
                        }
                    })
                };
            }
            else // resources/read response
            {
                return new McpJsonRpcMessage
                {
                    Id = 2,
                    Result = JsonSerializer.SerializeToElement(new
                    {
                        contents = new[] { new { text = "Hello World", mimeType = "text/markdown" } }
                    })
                };
            }
        });

        using var client = new McpClient(transport, "test-server");
        var provider = new McpResourceProvider(client);

        var docs = await provider.GetContextAsync();

        docs.Should().HaveCount(1);
        docs[0].Name.Should().Be("doc");
        docs[0].Content.Should().Be("Hello World");
        docs[0].Source.Should().Be("file:///doc.md");
        docs[0].Metadata["mimeType"].Should().Be("text/markdown");
        docs[0].Metadata["server"].Should().Be("test-server");
    }

    [Fact]
    public async Task GetContextAsync_EmptyResources_ReturnsEmpty()
    {
        var transport = Substitute.For<IMcpTransport>();
        transport.ReceiveAsync(Arg.Any<CancellationToken>()).Returns(new McpJsonRpcMessage
        {
            Id = 1,
            Result = JsonSerializer.SerializeToElement(new { resources = Array.Empty<object>() })
        });

        using var client = new McpClient(transport, "empty-server");
        var provider = new McpResourceProvider(client);

        var docs = await provider.GetContextAsync();
        docs.Should().BeEmpty();
    }
}
