using FluentAssertions;
using WorkflowFramework.Triggers;
using WorkflowFramework.Triggers.Sources;
using Xunit;

namespace WorkflowFramework.Tests.Triggers;

public class ScheduleTriggerSourceTests
{
    private static TriggerDefinition MakeDef(string cron = "* * * * *") => new()
    {
        Type = "schedule",
        Configuration = new Dictionary<string, string> { ["cronExpression"] = cron }
    };

    [Fact]
    public void Type_IsSchedule()
    {
        new ScheduleTriggerSource(MakeDef()).Type.Should().Be("schedule");
    }

    [Fact]
    public void DisplayName_IsSet()
    {
        new ScheduleTriggerSource(MakeDef()).DisplayName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task StartAsync_SetsIsRunning()
    {
        var source = new ScheduleTriggerSource(MakeDef());
        var ctx = new TriggerContext
        {
            WorkflowId = "wf1",
            Configuration = MakeDef().Configuration,
            OnTriggered = _ => Task.FromResult("run1")
        };
        await source.StartAsync(ctx);
        source.IsRunning.Should().BeTrue();
        await source.DisposeAsync();
    }

    [Fact]
    public async Task StopAsync_ClearsIsRunning()
    {
        var source = new ScheduleTriggerSource(MakeDef());
        var ctx = new TriggerContext
        {
            WorkflowId = "wf1",
            Configuration = MakeDef().Configuration,
            OnTriggered = _ => Task.FromResult("run1")
        };
        await source.StartAsync(ctx);
        await source.StopAsync();
        source.IsRunning.Should().BeFalse();
        await source.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_MissingCron_Throws()
    {
        var source = new ScheduleTriggerSource(new TriggerDefinition { Type = "schedule" });
        var ctx = new TriggerContext
        {
            WorkflowId = "wf1",
            Configuration = new Dictionary<string, string>(),
            OnTriggered = _ => Task.FromResult("run1")
        };
        var act = () => source.StartAsync(ctx);
        await act.Should().ThrowAsync<InvalidOperationException>();
        await source.DisposeAsync();
    }

    [Fact]
    public async Task StartAsync_InvalidCron_Throws()
    {
        var source = new ScheduleTriggerSource(MakeDef("bad"));
        var ctx = new TriggerContext
        {
            WorkflowId = "wf1",
            Configuration = new Dictionary<string, string> { ["cronExpression"] = "bad" },
            OnTriggered = _ => Task.FromResult("run1")
        };
        var act = () => source.StartAsync(ctx);
        await act.Should().ThrowAsync<FormatException>();
        await source.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        var source = new ScheduleTriggerSource(MakeDef());
        await source.DisposeAsync();
        await source.DisposeAsync(); // Should not throw
    }

    [Fact]
    public void Constructor_NullDefinition_Throws()
    {
        var act = () => new ScheduleTriggerSource(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
