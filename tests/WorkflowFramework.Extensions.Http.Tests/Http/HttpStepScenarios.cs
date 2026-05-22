using FluentAssertions;
using System.Net;
using System.Net.Http;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Http;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Http.Tests.Http;

[Feature("HttpStep — workflow step that makes HTTP requests")]
public class HttpStepScenarios : TinyBddXunitBase
{
    public HttpStepScenarios(ITestOutputHelper output) : base(output) { }

    // ── helpers ──────────────────────────────────────────────────────────

    private static HttpClient MakeFakeClient(HttpStatusCode status, string body)
    {
        var handler = new FakeHttpMessageHandler(status, body);
        return new HttpClient(handler);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public FakeHttpMessageHandler(HttpStatusCode status, string body) { _status = status; _body = body; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body)
            };
            return Task.FromResult(response);
        }
    }

    // ── Name ─────────────────────────────────────────────────────────────

    [Scenario("Default step name uses HTTP method when no name set"), Fact]
    public async Task DefaultStepName_UsesMethod()
    {
        var step = new HttpStep(new HttpStepOptions { Url = "http://example.com", Method = HttpMethod.Get });

        await Given("HttpStep with no explicit name", () => step.Name)
            .Then("name contains 'Http' (method-based default)", name =>
            {
                name.Should().StartWith("Http");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Custom step name from options is used"), Fact]
    public async Task CustomStepName_FromOptions()
    {
        var step = new HttpStep(new HttpStepOptions { Name = "Fetch", Url = "http://example.com" });

        await Given("HttpStepOptions.Name = 'Fetch'", () => step.Name)
            .Then("Name is 'Fetch'", name =>
            {
                name.Should().Be("Fetch");
                return true;
            })
            .AssertPassed();
    }

    // ── ExecuteAsync ──────────────────────────────────────────────────────

    [Scenario("ExecuteAsync on 200 response stores StatusCode and body in context"), Fact]
    public async Task ExecuteAsync_200_StoresStatusAndBody()
    {
        var client = MakeFakeClient(HttpStatusCode.OK, "hello");
        var step = new HttpStep(new HttpStepOptions
        {
            Name = "Get",
            Url = "http://fake/",
            Method = HttpMethod.Get,
            EnsureSuccessStatusCode = false
        }, client);
        var context = new WorkflowContext();

        await step.ExecuteAsync(context);

        await Given("HTTP 200 response with body 'hello'", () => context.Properties)
            .Then("context contains StatusCode=200 and body='hello'", props =>
            {
                props["Get.StatusCode"].Should().Be(200);
                props["Get.Body"].Should().Be("hello");
                props["Get.IsSuccess"].Should().Be(true);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ExecuteAsync stores IsSuccess=false for 404 response"), Fact]
    public async Task ExecuteAsync_404_IsSuccessFalse()
    {
        var client = MakeFakeClient(HttpStatusCode.NotFound, "not found");
        var step = new HttpStep(new HttpStepOptions
        {
            Name = "NotFoundStep",
            Url = "http://fake/",
            EnsureSuccessStatusCode = false
        }, client);
        var context = new WorkflowContext();

        await step.ExecuteAsync(context);

        await Given("HTTP 404 response", () => context.Properties["NotFoundStep.IsSuccess"])
            .Then("IsSuccess is false", isSuccess =>
            {
                isSuccess.Should().Be(false);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ExecuteAsync throws HttpRequestException on failure when EnsureSuccessStatusCode=true"), Fact]
    public async Task ExecuteAsync_EnsureSuccess_ThrowsOn500()
    {
        var client = MakeFakeClient(HttpStatusCode.InternalServerError, "error");
        var step = new HttpStep(new HttpStepOptions
        {
            Name = "S",
            Url = "http://fake/",
            EnsureSuccessStatusCode = true
        }, client);
        var context = new WorkflowContext();

        Exception? caught = null;
        try { await step.ExecuteAsync(context); }
        catch (Exception ex) { caught = ex; }

        await Given("EnsureSuccessStatusCode=true with 500 response", () => caught)
            .Then("HttpRequestException is thrown", ex =>
            {
                ex.Should().BeOfType<HttpRequestException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null options throws ArgumentNullException"), Fact]
    public async Task NullOptions_ThrowsArgumentNullException()
    {
        Exception? caught = null;
        try { _ = new HttpStep(null!); }
        catch (Exception ex) { caught = ex; }

        await Given("null options passed to HttpStep", () => caught)
            .Then("ArgumentNullException thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }
}
