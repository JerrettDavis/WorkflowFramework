using System.Net;
using FluentAssertions;
using WorkflowFramework.Extensions.Http;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Http;

public class HttpStepExtendedTests
{
    private static HttpClient CreateMockClient(HttpStatusCode status, string body = "", Dictionary<string, string>? responseHeaders = null)
    {
        var handler = new MockHttpHandler(status, body, responseHeaders);
        return new HttpClient(handler);
    }

    [Fact]
    public async Task ExecuteAsync_Get_SetsContextProperties()
    {
        var client = CreateMockClient(HttpStatusCode.OK, """{"result":"ok"}""");
        var step = new HttpStep(new HttpStepOptions
        {
            Url = "http://example.com/api",
            Method = HttpMethod.Get
        }, client);

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        context.Properties["HttpGET.StatusCode"].Should().Be(200);
        context.Properties["HttpGET.Body"].Should().Be("""{"result":"ok"}""");
        context.Properties["HttpGET.IsSuccess"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_Post_WithBody()
    {
        var client = CreateMockClient(HttpStatusCode.Created, "created");
        var step = new HttpStep(new HttpStepOptions
        {
            Url = "http://example.com/api",
            Method = HttpMethod.Post,
            Body = """{"name":"test"}""",
            ContentType = "application/json"
        }, client);

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        context.Properties["HttpPOST.StatusCode"].Should().Be(201);
        context.Properties["HttpPOST.IsSuccess"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_Put()
    {
        var client = CreateMockClient(HttpStatusCode.OK, "updated");
        var step = new HttpStep(new HttpStepOptions
        {
            Url = "http://example.com/api/1",
            Method = HttpMethod.Put,
            Body = """{"name":"updated"}"""
        }, client);

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        context.Properties["HttpPUT.StatusCode"].Should().Be(200);
    }

    [Fact]
    public async Task ExecuteAsync_Delete()
    {
        var client = CreateMockClient(HttpStatusCode.NoContent, "");
        var step = new HttpStep(new HttpStepOptions
        {
            Url = "http://example.com/api/1",
            Method = HttpMethod.Delete,
            EnsureSuccessStatusCode = false
        }, client);

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        context.Properties["HttpDELETE.StatusCode"].Should().Be(204);
    }

    [Fact]
    public async Task ExecuteAsync_WithHeaders()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, "ok");
        var client = new HttpClient(handler);
        var step = new HttpStep(new HttpStepOptions
        {
            Url = "http://example.com/api",
            Method = HttpMethod.Get,
            Headers = new Dictionary<string, string>
            {
                ["X-Custom"] = "value",
                ["Authorization"] = "Bearer token123"
            }
        }, client);

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        handler.LastRequest!.Headers.GetValues("X-Custom").Should().Contain("value");
        handler.LastRequest.Headers.GetValues("Authorization").Should().Contain("Bearer token123");
    }

    [Fact]
    public async Task ExecuteAsync_ErrorStatus_WithEnsureSuccess_Throws()
    {
        var client = CreateMockClient(HttpStatusCode.InternalServerError, "error");
        var step = new HttpStep(new HttpStepOptions
        {
            Url = "http://example.com/api",
            Method = HttpMethod.Get,
            EnsureSuccessStatusCode = true
        }, client);

        var context = new WorkflowContext();
        var act = () => step.ExecuteAsync(context);
        await act.Should().ThrowAsync<HttpRequestException>();

        // Properties should still be set before the throw
        context.Properties["HttpGET.StatusCode"].Should().Be(500);
        context.Properties["HttpGET.IsSuccess"].Should().Be(false);
    }

    [Fact]
    public async Task ExecuteAsync_ErrorStatus_WithoutEnsureSuccess_DoesNotThrow()
    {
        var client = CreateMockClient(HttpStatusCode.BadRequest, "bad");
        var step = new HttpStep(new HttpStepOptions
        {
            Url = "http://example.com/api",
            Method = HttpMethod.Get,
            EnsureSuccessStatusCode = false
        }, client);

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        context.Properties["HttpGET.StatusCode"].Should().Be(400);
        context.Properties["HttpGET.IsSuccess"].Should().Be(false);
    }

    [Fact]
    public async Task ExecuteAsync_CustomName_UsedInProperties()
    {
        var client = CreateMockClient(HttpStatusCode.OK, "ok");
        var step = new HttpStep(new HttpStepOptions
        {
            Name = "FetchUser",
            Url = "http://example.com/api",
            Method = HttpMethod.Get
        }, client);

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        context.Properties.Should().ContainKey("FetchUser.StatusCode");
        context.Properties.Should().ContainKey("FetchUser.Body");
        context.Properties.Should().ContainKey("FetchUser.IsSuccess");
    }

    [Fact]
    public async Task ExecuteAsync_NoBody_DoesNotSetContent()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, "ok");
        var client = new HttpClient(handler);
        var step = new HttpStep(new HttpStepOptions
        {
            Url = "http://example.com/api",
            Method = HttpMethod.Get
        }, client);

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        handler.LastRequest!.Content.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_NullContentType_UsesJson()
    {
        var handler = new CapturingHttpHandler(HttpStatusCode.OK, "ok");
        var client = new HttpClient(handler);
        var step = new HttpStep(new HttpStepOptions
        {
            Url = "http://example.com/api",
            Method = HttpMethod.Post,
            Body = "data",
            ContentType = null
        }, client);

        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        handler.LastRequest!.Content.Should().NotBeNull();
    }

    private class MockHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public MockHttpHandler(HttpStatusCode status, string body, Dictionary<string, string>? headers = null)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body)
            };
            return Task.FromResult(response);
        }
    }

    private class CapturingHttpHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body)
            };
            return Task.FromResult(response);
        }
    }
}
