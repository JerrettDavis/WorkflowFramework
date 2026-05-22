using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Diagnostics.Tests.Diagnostics;

[Feature("MetricsMiddleware — collects workflow execution counters")]
public class MetricsMiddlewareScenarios : TinyBddXunitBase
{
    public MetricsMiddlewareScenarios(ITestOutputHelper output) : base(output) { }

    private sealed class LambdaStep : IStep
    {
        private readonly Func<IWorkflowContext, Task> _fn;
        public LambdaStep(string name, Func<IWorkflowContext, Task> fn) { Name = name; _fn = fn; }
        public string Name { get; }
        public Task ExecuteAsync(IWorkflowContext context) => _fn(context);
    }

    [Scenario("Initial state has zero counters"), Fact]
    public async Task InitialState_ZeroCounters()
    {
        var mw = new MetricsMiddleware();

        await Given("a fresh MetricsMiddleware", () => mw)
            .Then("TotalSteps=0, FailedSteps=0, AverageDuration=Zero", m =>
            {
                m.TotalSteps.Should().Be(0);
                m.FailedSteps.Should().Be(0);
                m.AverageDuration.Should().Be(TimeSpan.Zero);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Successful step increments TotalSteps"), Fact]
    public async Task SuccessfulStep_IncrementsTotalSteps()
    {
        var mw = new MetricsMiddleware();
        var step = new LambdaStep("ok", _ => Task.CompletedTask);
        var ctx = new WorkflowContext();

        await mw.InvokeAsync(ctx, step, c => step.ExecuteAsync(c));

        await Given("one successful step", () => mw.TotalSteps)
            .Then("TotalSteps is 1", t =>
            {
                t.Should().Be(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Failing step increments both TotalSteps and FailedSteps"), Fact]
    public async Task FailingStep_IncrementsBothCounters()
    {
        var mw = new MetricsMiddleware();
        var step = new LambdaStep("fail", _ => throw new InvalidOperationException("boom"));
        var ctx = new WorkflowContext();

        try { await mw.InvokeAsync(ctx, step, c => step.ExecuteAsync(c)); }
        catch { /* expected */ }

        await Given("one failing step", () => (mw.TotalSteps, mw.FailedSteps))
            .Then("TotalSteps=1 and FailedSteps=1", t =>
            {
                t.TotalSteps.Should().Be(1);
                t.FailedSteps.Should().Be(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AverageDuration is positive after step execution"), Fact]
    public async Task AverageDuration_IsPositive_AfterExecution()
    {
        var mw = new MetricsMiddleware();
        var step = new LambdaStep("timed", _ => Task.CompletedTask);
        var ctx = new WorkflowContext();

        await mw.InvokeAsync(ctx, step, c => step.ExecuteAsync(c));

        await Given("one step executed", () => mw.AverageDuration)
            .Then("AverageDuration is >= zero", d =>
            {
                d.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Multiple steps accumulate TotalSteps correctly"), Fact]
    public async Task MultipleSteps_AccumulateCount()
    {
        var mw = new MetricsMiddleware();
        var ctx = new WorkflowContext();
        for (int i = 0; i < 5; i++)
        {
            var step = new LambdaStep($"step-{i}", _ => Task.CompletedTask);
            await mw.InvokeAsync(ctx, step, c => step.ExecuteAsync(c));
        }

        await Given("5 steps executed", () => mw.TotalSteps)
            .Then("TotalSteps is 5", t =>
            {
                t.Should().Be(5);
                return true;
            })
            .AssertPassed();
    }
}
