using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Diagnostics.Tests.Diagnostics;

[Feature("TimingMiddleware — measures and stores step execution time")]
public class TimingMiddlewareScenarios : TinyBddXunitBase
{
    public TimingMiddlewareScenarios(ITestOutputHelper output) : base(output) { }

    private static IStep MakeStep(string name, int delayMs = 0) =>
        new LambdaStep(name, async ctx =>
        {
            if (delayMs > 0) await Task.Delay(delayMs);
        });

    private sealed class LambdaStep : IStep
    {
        private readonly Func<IWorkflowContext, Task> _fn;
        public LambdaStep(string name, Func<IWorkflowContext, Task> fn) { Name = name; _fn = fn; }
        public string Name { get; }
        public Task ExecuteAsync(IWorkflowContext context) => _fn(context);
    }

    [Scenario("TimingsKey constant has expected value"), Fact]
    public async Task TimingsKey_HasExpectedValue()
    {
        await Given("TimingMiddleware.TimingsKey constant", () => TimingMiddleware.TimingsKey)
            .Then("value is 'WorkflowFramework.StepTimings'", k =>
            {
                k.Should().Be("WorkflowFramework.StepTimings");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("InvokeAsync stores timing for the step"), Fact]
    public async Task InvokeAsync_StoresTiming()
    {
        var middleware = new TimingMiddleware();
        var context = new WorkflowContext();
        var step = MakeStep("my-step");

        await middleware.InvokeAsync(context, step, ctx => step.ExecuteAsync(ctx));

        await Given("TimingMiddleware applied to my-step", () => context.Properties)
            .Then("timings dictionary contains 'my-step'", props =>
            {
                props.Should().ContainKey(TimingMiddleware.TimingsKey);
                var timings = (Dictionary<string, TimeSpan>)props[TimingMiddleware.TimingsKey];
                timings.Should().ContainKey("my-step");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Multiple steps each get their own timing entry"), Fact]
    public async Task MultipleSteps_EachGetTimingEntry()
    {
        var middleware = new TimingMiddleware();
        var context = new WorkflowContext();
        var step1 = MakeStep("step-a");
        var step2 = MakeStep("step-b");

        await middleware.InvokeAsync(context, step1, ctx => step1.ExecuteAsync(ctx));
        await middleware.InvokeAsync(context, step2, ctx => step2.ExecuteAsync(ctx));

        await Given("two steps both measured by TimingMiddleware", () => context.Properties[TimingMiddleware.TimingsKey])
            .Then("both entries exist in timings", obj =>
            {
                var timings = (Dictionary<string, TimeSpan>)obj!;
                timings.Should().ContainKey("step-a").And.ContainKey("step-b");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Timing is still recorded when step throws"), Fact]
    public async Task InvokeAsync_StillRecordsTiming_WhenStepThrows()
    {
        var middleware = new TimingMiddleware();
        var context = new WorkflowContext();
        var step = new LambdaStep("throwing-step", _ => throw new InvalidOperationException("boom"));

        try { await middleware.InvokeAsync(context, step, ctx => step.ExecuteAsync(ctx)); }
        catch { /* expected */ }

        await Given("step throws during execution", () => context.Properties)
            .Then("timing entry still recorded (finally block)", props =>
            {
                props.Should().ContainKey(TimingMiddleware.TimingsKey);
                var timings = (Dictionary<string, TimeSpan>)props[TimingMiddleware.TimingsKey];
                timings.Should().ContainKey("throwing-step");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Recorded elapsed time is non-negative"), Fact]
    public async Task RecordedElapsedTime_IsNonNegative()
    {
        var middleware = new TimingMiddleware();
        var context = new WorkflowContext();
        var step = MakeStep("quick");

        await middleware.InvokeAsync(context, step, ctx => step.ExecuteAsync(ctx));

        var timings = (Dictionary<string, TimeSpan>)context.Properties[TimingMiddleware.TimingsKey];

        await Given("step executed and timed", () => timings["quick"])
            .Then("elapsed is >= 0", elapsed =>
            {
                elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
                return true;
            })
            .AssertPassed();
    }
}
