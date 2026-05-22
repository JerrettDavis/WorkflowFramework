using FluentAssertions;
using Polly;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Polly.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Polly.Tests;

[Feature("WorkflowBuilderPollyExtensions")]
public class WorkflowBuilderPollyExtensionsScenarios : PollyTestBase
{
    public WorkflowBuilderPollyExtensionsScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("UseResilience with a pre-built pipeline registers ResilienceMiddleware"), Fact]
    public async Task UseResilienceRegistersMiddlewareFromPreBuiltPipeline()
    {
        var pipeline = new ResiliencePipelineBuilder().Build();
        var executed = false;

        var workflow = Workflow.Create("ext-prebuilt")
            .UseResilience(pipeline)
            .Step("step", _ => { executed = true; return Task.CompletedTask; })
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        await Given("UseResilience with a pre-built pipeline", () => (result, executed))
            .Then("step runs and workflow succeeds", t =>
            {
                t.executed.Should().BeTrue();
                t.result.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("UseResilience with action-based builder configures middleware"), Fact]
    public async Task UseResilienceWithActionConfiguresPipeline()
    {
        var executed = false;

        var workflow = Workflow.Create("ext-action")
            .UseResilience(b => { /* no-op pipeline */ })
            .Step("step", _ => { executed = true; return Task.CompletedTask; })
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        await Given("UseResilience with an action-based builder", () => (result, executed))
            .Then("step runs and workflow succeeds", t =>
            {
                t.executed.Should().BeTrue();
                t.result.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }
}
