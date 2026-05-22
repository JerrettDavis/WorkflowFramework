using FluentAssertions;
using Polly;
using Polly.Retry;
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

    [Scenario("UseResilience with null pipeline overload throws ArgumentNullException"), Fact]
    public async Task UseResilienceNullPipelineThrows()
    {
        Exception? caught = null;
        try
        {
            Workflow.Create("ext-null")
                .UseResilience((ResiliencePipeline)null!)
                .Build();
        }
        catch (Exception ex) { caught = ex; }

        await Given("UseResilience called with null pipeline", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull();
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("UseResilience with action enables retry and recovers from transient step failure"), Fact]
    public async Task UseResilienceActionEnablesRetryRecovery()
    {
        var attempts = 0;
        var workflow = Workflow.Create("ext-retry")
            .UseResilience(b => b.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
                Delay = TimeSpan.Zero
            }))
            .Step("flaky", _ =>
            {
                attempts++;
                if (attempts < 3) throw new InvalidOperationException("transient");
                return Task.CompletedTask;
            })
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        await Given("UseResilience action with retry and a step that fails twice", () => (result, attempts))
            .Then("3 attempts are made and workflow succeeds", t =>
            {
                t.attempts.Should().Be(3);
                t.result.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("UseResilience can be chained fluently before other builder calls"), Fact]
    public async Task UseResilienceReturnsSameBuilderForChaining()
    {
        var order = new List<string>();
        var pipeline = new ResiliencePipelineBuilder().Build();

        var workflow = Workflow.Create("ext-chain")
            .UseResilience(pipeline)
            .Step("first", _ => { order.Add("first"); return Task.CompletedTask; })
            .Step("second", _ => { order.Add("second"); return Task.CompletedTask; })
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        await Given("UseResilience followed by two steps", () => (result, order))
            .Then("both steps execute in order", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.order.Should().Equal("first", "second");
                return true;
            })
            .AssertPassed();
    }
}
