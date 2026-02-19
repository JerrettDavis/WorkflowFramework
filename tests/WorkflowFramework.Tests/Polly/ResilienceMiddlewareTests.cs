using FluentAssertions;
using Polly;
using Polly.Retry;
using WorkflowFramework.Builder;
using WorkflowFramework.Extensions.Polly;
using Xunit;

namespace WorkflowFramework.Tests.Polly;

public class ResilienceMiddlewareTests
{
    [Fact]
    public void Constructor_NullPipeline_Throws()
    {
        var act = () => new ResilienceMiddleware(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task InvokeAsync_ExecutesThroughPipeline()
    {
        var pipeline = new ResiliencePipelineBuilder().Build();
        var middleware = new ResilienceMiddleware(pipeline);
        var executed = false;
        var context = new WorkflowContext();
        var step = new TestStep("test");
        await middleware.InvokeAsync(context, step, ctx => { executed = true; return Task.CompletedTask; });
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithRetry_RetriesOnFailure()
    {
        var attempts = 0;
        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new global::Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.Zero,
                ShouldHandle = new PredicateBuilder().Handle<InvalidOperationException>()
            })
            .Build();
        var middleware = new ResilienceMiddleware(pipeline);
        var context = new WorkflowContext();
        var step = new TestStep("test");
        await middleware.InvokeAsync(context, step, ctx =>
        {
            attempts++;
            if (attempts < 3) throw new InvalidOperationException("fail");
            return Task.CompletedTask;
        });
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task UseResilience_WithPipeline_AddsMiddleware()
    {
        var pipeline = new ResiliencePipelineBuilder().Build();
        var workflow = new WorkflowBuilder()
            .WithName("Test")
            .UseResilience(pipeline)
            .Step("s1", ctx => { ctx.Properties["ran"] = true; return Task.CompletedTask; })
            .Build();
        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);
        context.Properties["ran"].Should().Be(true);
    }

    [Fact]
    public async Task UseResilience_WithBuilderAction_AddsMiddleware()
    {
        var workflow = new WorkflowBuilder()
            .WithName("Test")
            .UseResilience(b => { })
            .Step("s1", ctx => { ctx.Properties["ran"] = true; return Task.CompletedTask; })
            .Build();
        var context = new WorkflowContext();
        await workflow.ExecuteAsync(context);
        context.Properties["ran"].Should().Be(true);
    }

    private sealed class TestStep(string name) : IStep
    {
        public string Name { get; } = name;
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }
}
