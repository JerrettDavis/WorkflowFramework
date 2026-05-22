using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Triggers;
using WorkflowFramework.Triggers.Sources;
using WorkflowFramework.Tests.TinyBDD.Support;

namespace WorkflowFramework.Tests.TinyBDD.Core.Triggers;

[Feature("ManualTriggerSource behaviour")]
public class ManualTriggerSourceTests : TinyBddTestBase
{
    public ManualTriggerSourceTests(ITestOutputHelper output) : base(output) { }

    [Scenario("IsRunning is false before Start and true after Start"), Fact]
    public async Task IsRunningReflectsLifecycle()
    {
        var def = new TriggerDefinition { Type = "manual" };
        var src = new ManualTriggerSource(def);
        var beforeStart = src.IsRunning;
        await src.StartAsync(BuildContext());
        var afterStart = src.IsRunning;

        await Given("IsRunning state before and after StartAsync", () => (beforeStart, afterStart))
            .Then("IsRunning transitions from false to true", t =>
            {
                t.beforeStart.Should().BeFalse();
                t.afterStart.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("IsRunning becomes false after StopAsync"), Fact]
    public async Task StopSetsIsRunningFalse()
    {
        var def = new TriggerDefinition { Type = "manual" };
        var src = new ManualTriggerSource(def);
        await src.StartAsync(BuildContext());
        await src.StopAsync();
        var isRunning = src.IsRunning;

        await Given("a stopped manual trigger", () => isRunning)
            .Then("IsRunning is false", r =>
            {
                r.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("FireAsync invokes the OnTriggered callback"), Fact]
    public async Task FireAsyncInvokesCallback()
    {
        var def = new TriggerDefinition { Type = "manual" };
        var src = new ManualTriggerSource(def);
        var events = new List<TriggerEvent>();
        var ctx = new TriggerContext
        {
            WorkflowId = "wf-manual",
            Configuration = new Dictionary<string, string>(),
            OnTriggered = e => { events.Add(e); return Task.FromResult("run-1"); }
        };
        await src.StartAsync(ctx);
        await src.FireAsync(new Dictionary<string, object> { ["key"] = "hello" });

        await Given("the events captured after FireAsync", () => events)
            .Then("one event with TriggerType 'manual' was recorded", evts =>
            {
                evts.Should().ContainSingle();
                evts[0].TriggerType.Should().Be("manual");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("FireAsync before Start throws InvalidOperationException"), Fact]
    public async Task FireBeforeStartThrows()
    {
        var def = new TriggerDefinition { Type = "manual" };
        var src = new ManualTriggerSource(def);
        Exception? thrown = null;
        try { await src.FireAsync(); }
        catch (InvalidOperationException ex) { thrown = ex; }

        await Given("an exception thrown by FireAsync before Start", () => thrown)
            .Then("the exception is an InvalidOperationException", ex =>
            {
                ex.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    private static TriggerContext BuildContext(string wfId = "wf-test") =>
        new()
        {
            WorkflowId = wfId,
            Configuration = new Dictionary<string, string>(),
            OnTriggered = _ => Task.FromResult("run-id")
        };
}
