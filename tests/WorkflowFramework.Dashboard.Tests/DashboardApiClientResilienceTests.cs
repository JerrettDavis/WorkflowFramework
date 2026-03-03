#if NET10_0
using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using WorkflowFramework.Dashboard.Web.Models;
using WorkflowFramework.Dashboard.Web.Services;
using Xunit;

namespace WorkflowFramework.Dashboard.Tests;

public sealed class DashboardApiClientResilienceTests
{
    [Fact]
    public void GetExecutionHubUrls_ExpandsServiceDiscoveryScheme()
    {
        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("https+http://dashboard-api")
        };

        var client = new DashboardApiClient(http);
        var urls = client.GetExecutionHubUrls();

        urls.Should().Contain("https://dashboard-api/hubs/execution");
        urls.Should().Contain("http://dashboard-api/hubs/execution");
    }

    [Fact]
    public async Task ValidateDefinitionAsync_ParsesSeverityObjectFallback()
    {
        const string payload = """
                               {
                                 "errors": [
                                   { "severity": { "name": "Warning" }, "stepName": "StepA", "message": "Check this" }
                                 ],
                                 "isValid": true
                               }
                               """;

        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        }))
        {
            BaseAddress = new Uri("http://localhost")
        };

        var client = new DashboardApiClient(http);
        var result = await client.ValidateDefinitionAsync(new WorkflowDefinitionDto { Name = "Demo" });

        result.Should().NotBeNull();
        result!.Errors.Should().HaveCount(1);
        result.Errors[0].Severity.Should().Be("Warning");
        result.Errors[0].StepName.Should().Be("StepA");
    }

    [Fact]
    public async Task GetRunAsync_ReturnsNull_On404()
    {
        using var http = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)))
        {
            BaseAddress = new Uri("http://localhost")
        };

        var client = new DashboardApiClient(http);
        var run = await client.GetRunAsync("missing-run");

        run.Should().BeNull();
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
#endif
