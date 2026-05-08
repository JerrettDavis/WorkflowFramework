using System.Net;
using System.Reflection;
using System.Text;
using FluentAssertions;
using WorkflowFramework.Extensions.Agents.Mcp;
using Xunit;

namespace WorkflowFramework.Tests.Agents.Mcp;

public class HttpMcpTransportIoTests
{
    [Fact]
    public async Task SendAsync_WithSseResponse_QueuesMessage()
    {
        using var transport = new HttpMcpTransport("https://example.test/mcp");
        SetHttpClient(transport, CreateHttpClient("""
data: {"jsonrpc":"2.0","method":"tools/list"}
data: [DONE]
""", "text/event-stream"));

        await transport.SendAsync(new McpJsonRpcMessage { Method = "initialize" });
        var message = await transport.ReceiveAsync();

        message.Method.Should().Be("tools/list");
    }

    [Fact]
    public async Task SendAsync_WithPlainJsonResponse_QueuesMessage()
    {
        using var transport = new HttpMcpTransport("https://example.test/mcp");
        SetHttpClient(transport, CreateHttpClient("""{"jsonrpc":"2.0","method":"resources/list"}""", "application/json"));

        await transport.SendAsync(new McpJsonRpcMessage { Method = "initialize" });
        var message = await transport.ReceiveAsync();

        message.Method.Should().Be("resources/list");
    }

    [Fact]
    public async Task SendAsync_WithNonSuccessStatus_Throws()
    {
        using var transport = new HttpMcpTransport("https://example.test/mcp");
        SetHttpClient(transport, new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadGateway))));

        var act = async () => await transport.SendAsync(new McpJsonRpcMessage { Method = "initialize" });

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private static HttpClient CreateHttpClient(string responseBody, string mediaType)
    {
        return new HttpClient(new StubHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, mediaType)
            }));
    }

    private static void SetHttpClient(HttpMcpTransport transport, HttpClient httpClient)
    {
        var field = typeof(HttpMcpTransport).GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        field!.SetValue(transport, httpClient);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
