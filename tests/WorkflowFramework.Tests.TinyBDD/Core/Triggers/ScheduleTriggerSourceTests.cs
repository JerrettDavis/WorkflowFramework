using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Triggers;
using WorkflowFramework.Triggers.Sources;
using WorkflowFramework.Tests.TinyBDD.Support;

namespace WorkflowFramework.Tests.TinyBDD.Core.Triggers;

[Feature("ScheduleTriggerSource lifecycle")]
public class ScheduleTriggerSourceTests : TinyBddTestBase
{
    public ScheduleTriggerSourceTests(ITestOutputHelper output) : base(output) { }

    [Scenario("StartAsync with a valid cron expression sets IsRunning to true"), Fact]
    public async Task StartWithValidCronSetsIsRunning()
    {
        var def = new TriggerDefinition { Type = "schedule" };
        var src = new ScheduleTriggerSource(def);
        var ctx = new TriggerContext
        {
            WorkflowId = "sched-wf",
            Configuration = new Dictionary<string, string> { ["cronExpression"] = "0 * * * *" },
            OnTriggered = _ => Task.FromResult("run-id")
        };
        await src.StartAsync(ctx);
        var isRunning = src.IsRunning;
        await src.StopAsync();
        await src.DisposeAsync();

        await Given("IsRunning after StartAsync", () => isRunning)
            .Then("IsRunning is true", r =>
            {
                r.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("StopAsync sets IsRunning to false"), Fact]
    public async Task StopSetsIsRunningFalse()
    {
        var def = new TriggerDefinition { Type = "schedule" };
        var src = new ScheduleTriggerSource(def);
        var ctx = new TriggerContext
        {
            WorkflowId = "sched-stop",
            Configuration = new Dictionary<string, string> { ["cronExpression"] = "0 0 * * *" },
            OnTriggered = _ => Task.FromResult("run-id")
        };
        await src.StartAsync(ctx);
        await src.StopAsync();
        var isRunning = src.IsRunning;
        await src.DisposeAsync();

        await Given("IsRunning after StopAsync", () => isRunning)
            .Then("IsRunning is false", r =>
            {
                r.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("StartAsync without cronExpression in config throws InvalidOperationException"), Fact]
    public async Task StartWithoutCronThrows()
    {
        var def = new TriggerDefinition { Type = "schedule" };
        var src = new ScheduleTriggerSource(def);
        var ctx = new TriggerContext
        {
            WorkflowId = "sched-bad",
            Configuration = new Dictionary<string, string>(),
            OnTriggered = _ => Task.FromResult("run-id")
        };
        Exception? thrown = null;
        try { await src.StartAsync(ctx); }
        catch (InvalidOperationException ex) { thrown = ex; }

        await Given("an exception thrown by StartAsync with missing cron config", () => thrown)
            .Then("an InvalidOperationException is thrown", ex =>
            {
                ex.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }
}
