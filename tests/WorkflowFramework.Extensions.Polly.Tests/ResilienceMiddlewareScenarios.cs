using FluentAssertions;
using Polly;
using Polly.Retry;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Polly.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Polly.Tests;

[Feature("ResilienceMiddleware")]
public class ResilienceMiddlewareScenarios : PollyTestBase
{
    public ResilienceMiddlewareScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("Middleware wraps step execution with a Polly pipeline and allows success"), Fact]
    public async Task MiddlewareAllowsSuccessfulStepToComplete()
    {
        var executed = false;
        var pipeline = new ResiliencePipelineBuilder().Build();
        var middleware = new ResilienceMiddleware(pipeline);

        var workflow = Workflow.Create("polly-pass")
            .Use(middleware)
            .Step("success-step", _ => { executed = true; return Task.CompletedTask; })
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        await Given("a workflow with Polly middleware and a passing step", () => (result, executed))
            .Then("the step executes and result is successful", t =>
            {
                t.executed.Should().BeTrue();
                t.result.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Middleware retries a transient fault and eventually succeeds"), Fact]
    public async Task MiddlewareRetriesTransientFault()
    {
        var attempts = 0;
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>(),
                Delay = TimeSpan.Zero
            })
            .Build();

        var middleware = new ResilienceMiddleware(pipeline);

        // fail twice, succeed on 3rd
        var workflow = Workflow.Create("polly-retry")
            .Use(middleware)
            .Step("flaky-step", _ =>
            {
                attempts++;
                if (attempts < 3) throw new InvalidOperationException("transient");
                return Task.CompletedTask;
            })
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        await Given("a flaky step that fails twice then succeeds", () => (result, attempts))
            .Then("the middleware retried and the workflow succeeded", t =>
            {
                t.attempts.Should().Be(3);
                t.result.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Middleware throws ArgumentNullException when pipeline is null"), Fact]
    public async Task MiddlewareRequiresPipeline()
    {
        Exception? caught = null;
        try { _ = new ResilienceMiddleware(null!); }
        catch (Exception ex) { caught = ex; }

        await Given("a null pipeline", () => caught)
            .Then("constructor throws ArgumentNullException", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Pipeline configured via builder action applies resilience to step"), Fact]
    public async Task BuilderExtensionAppliesResiliencePipelineFromAction()
    {
        var executed = false;
        var workflow = Workflow.Create("polly-builder")
            .UseResilience(b => b.AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 1 }))
            .Step("ok-step", _ => { executed = true; return Task.CompletedTask; })
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        await Given("a workflow using the builder extension with an action", () => (result, executed))
            .Then("the step executes successfully", t =>
            {
                t.executed.Should().BeTrue();
                t.result.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }
}
