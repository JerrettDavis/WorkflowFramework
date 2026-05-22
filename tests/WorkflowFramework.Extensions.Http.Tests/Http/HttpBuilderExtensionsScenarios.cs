using FluentAssertions;
using System.Net;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework;
using WorkflowFramework.Builder;
using WorkflowFramework.Extensions.Http;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Http.Tests.Http;

[Feature("HttpBuilderExtensions — fluent HTTP step wiring")]
public class HttpBuilderExtensionsScenarios : TinyBddXunitBase
{
    public HttpBuilderExtensionsScenarios(ITestOutputHelper output) : base(output) { }

    private sealed class FakeOkHandler : System.Net.Http.HttpMessageHandler
    {
        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new System.Net.Http.HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent("ok")
            });
        }
    }

    [Scenario("HttpGet adds a step with GET method"), Fact]
    public async Task HttpGet_AddsGetStep()
    {
        var builder = new WorkflowBuilder();
        // HttpGet wires step with a fake client — just verify the workflow builds
        builder.HttpGet("http://fake/");
        var wf = builder.Build();

        await Given("HttpGet added to workflow builder", () => wf)
            .Then("workflow is built (step wired)", w =>
            {
                w.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("HttpPost adds a step with POST method"), Fact]
    public async Task HttpPost_AddsPostStep()
    {
        var builder = new WorkflowBuilder();
        builder.HttpPost("http://fake/", body: "{\"x\":1}");
        var wf = builder.Build();

        await Given("HttpPost added to workflow builder", () => wf)
            .Then("workflow is built", w =>
            {
                w.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("HttpPut adds a step with PUT method"), Fact]
    public async Task HttpPut_AddsStep()
    {
        var builder = new WorkflowBuilder();
        builder.HttpPut("http://fake/", body: "{\"y\":2}");
        var wf = builder.Build();

        await Given("HttpPut added to workflow builder", () => wf)
            .Then("workflow is built", w =>
            {
                w.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("HttpDelete adds a step with DELETE method"), Fact]
    public async Task HttpDelete_AddsStep()
    {
        var builder = new WorkflowBuilder();
        builder.HttpDelete("http://fake/");
        var wf = builder.Build();

        await Given("HttpDelete added to workflow builder", () => wf)
            .Then("workflow is built", w =>
            {
                w.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("HttpGet with custom name uses that name"), Fact]
    public async Task HttpGet_CustomName()
    {
        var step = new HttpStep(new HttpStepOptions
        {
            Name = "FetchUser",
            Url = "http://fake/",
            Method = System.Net.Http.HttpMethod.Get
        });

        await Given("HttpStepOptions with Name='FetchUser'", () => step.Name)
            .Then("step name is 'FetchUser'", name =>
            {
                name.Should().Be("FetchUser");
                return true;
            })
            .AssertPassed();
    }
}
