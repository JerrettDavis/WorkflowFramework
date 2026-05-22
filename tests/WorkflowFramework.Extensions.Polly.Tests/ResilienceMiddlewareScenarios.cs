using FluentAssertions;
using Polly;
using Polly.Retry;
using Polly.Timeout;
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

    [Scenario("Retry exhaustion surfaces the last exception to the workflow result"), Fact]
    public async Task RetryExhaustionSurfaces()
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

        // Always fails — exhausts all retries
        var workflow = Workflow.Create("polly-exhaust")
            .Use(middleware)
            .Step("always-fail", _ =>
            {
                attempts++;
                throw new InvalidOperationException("always fail");
            })
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        await Given("a step that always throws InvalidOperationException", () => (result, attempts))
            .Then("3 attempts are made (1 + 2 retries) and workflow reports failure", t =>
            {
                t.attempts.Should().Be(3);
                t.result.IsSuccess.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Exception predicate filters: only matching exceptions are retried"), Fact]
    public async Task ExceptionPredicateFiltersOnMatchingType()
    {
        var attempts = 0;
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                // Only retry ArgumentException — NOT InvalidOperationException
                ShouldHandle = new PredicateBuilder().Handle<ArgumentException>(),
                Delay = TimeSpan.Zero
            })
            .Build();

        var middleware = new ResilienceMiddleware(pipeline);

        var workflow = Workflow.Create("polly-predicate")
            .Use(middleware)
            .Step("wrong-exception", _ =>
            {
                attempts++;
                throw new InvalidOperationException("not retried");
            })
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        await Given("a step throwing InvalidOperationException with an ArgumentException-only retry predicate", () => (result, attempts))
            .Then("only 1 attempt is made (no retries) and workflow fails", t =>
            {
                t.attempts.Should().Be(1);
                t.result.IsSuccess.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Cancellation token propagates through Polly pipeline and aborts the workflow"), Fact]
    public async Task CancellationPropagatesThroughPipeline()
    {
        // The engine catches OperationCanceledException and transitions to Aborted status.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var pipeline = new ResiliencePipelineBuilder().Build();
        var middleware = new ResilienceMiddleware(pipeline);

        var workflow = Workflow.Create("polly-cancel")
            .Use(middleware)
            .Step("slow-step", async ctx =>
            {
                await Task.Delay(5000, ctx.CancellationToken);
            })
            .Build();

        var context = new WorkflowContext(cts.Token);
        var result = await workflow.ExecuteAsync(context);

        await Given("a pre-cancelled token passed to a workflow using Polly middleware", () => result)
            .Then("the workflow is aborted", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Aborted);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Multiple middleware can be stacked around Polly middleware"), Fact]
    public async Task PollyMiddlewareComposesWithOtherMiddleware()
    {
        var order = new List<string>();
        var pipeline = new ResiliencePipelineBuilder().Build();

        // Custom bookend middleware
        var before = new DelegateMiddleware(async (ctx, step, next) =>
        {
            order.Add("before");
            await next(ctx);
            order.Add("after");
        });

        var workflow = Workflow.Create("polly-compose")
            .Use(before)
            .Use(new ResilienceMiddleware(pipeline))
            .Step("step", _ => { order.Add("step"); return Task.CompletedTask; })
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        await Given("Polly middleware sandwiched between custom middleware", () => (result, order))
            .Then("execution order is before -> step -> after and workflow succeeds", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.order.Should().Equal("before", "step", "after");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Middleware works correctly when step succeeds on first attempt with retry configured"), Fact]
    public async Task SuccessOnFirstAttemptWithRetryConfigured()
    {
        var attempts = 0;
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 5,
                Delay = TimeSpan.Zero
            })
            .Build();

        var workflow = Workflow.Create("polly-first-success")
            .Use(new ResilienceMiddleware(pipeline))
            .Step("step", _ => { attempts++; return Task.CompletedTask; })
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        await Given("a step that succeeds on first attempt with 5 retries configured", () => (result, attempts))
            .Then("only 1 attempt is made and workflow succeeds", t =>
            {
                t.attempts.Should().Be(1);
                t.result.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Middleware applies to every step in the workflow independently"), Fact]
    public async Task MiddlewareAppliedToEveryStep()
    {
        var stepACount = 0;
        var stepBCount = 0;
        var pipeline = new ResiliencePipelineBuilder().Build();
        var middleware = new ResilienceMiddleware(pipeline);

        var workflow = Workflow.Create("polly-multi-step")
            .Use(middleware)
            .Step("step-a", _ => { stepACount++; return Task.CompletedTask; })
            .Step("step-b", _ => { stepBCount++; return Task.CompletedTask; })
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        await Given("a workflow with two steps under Polly middleware", () => (result, stepACount, stepBCount))
            .Then("both steps execute once each and workflow succeeds", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.stepACount.Should().Be(1);
                t.stepBCount.Should().Be(1);
                return true;
            })
            .AssertPassed();
    }
}
