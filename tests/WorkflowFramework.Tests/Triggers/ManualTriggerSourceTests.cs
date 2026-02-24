using FluentAssertions;
using WorkflowFramework.Triggers;
using WorkflowFramework.Triggers.Sources;
using Xunit;

namespace WorkflowFramework.Tests.Triggers;

public class ManualTriggerSourceTests
{
    [Fact]
    public void Type_IsManual()
    {
        new ManualTriggerSource(new TriggerDefinition { Type = "manual" }).Type.Should().Be("manual");
    }

    [Fact]
    public async Task StartAsync_SetsIsRunning()
    {
        var source = new ManualTriggerSource(new TriggerDefinition { Type = "manual" });
        var ctx = new TriggerContext
        {
            WorkflowId = "wf1",
            Configuration = new Dictionary<string, string>(),
            OnTriggered = _ => Task.FromResult("run1")
        };
        await source.StartAsync(ctx);
        source.IsRunning.Should().BeTrue();
        await source.DisposeAsync();
    }

    [Fact]
    public async Task FireAsync_InvokesCallback()
    {
        TriggerEvent? captured = null;
        var source = new ManualTriggerSource(new TriggerDefinition { Type = "manual" });
        var ctx = new TriggerContext
        {
            WorkflowId = "wf1",
            Configuration = new Dictionary<string, string>(),
            OnTriggered = e => { captured = e; return Task.FromResult("run1"); }
        };
        await source.StartAsync(ctx);

        var runId = await source.FireAsync(
            new Dictionary<string, object> { ["key"] = "value" },
            "corr-123");

        runId.Should().Be("run1");
        captured.Should().NotBeNull();
        captured!.TriggerType.Should().Be("manual");
        captured.Payload["key"].Should().Be("value");
        captured.CorrelationId.Should().Be("corr-123");
        await source.DisposeAsync();
    }

    [Fact]
    public async Task FireAsync_BeforeStart_Throws()
    {
        var source = new ManualTriggerSource(new TriggerDefinition { Type = "manual" });
        var act = () => source.FireAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StopAsync_ClearsIsRunning()
    {
        var source = new ManualTriggerSource(new TriggerDefinition { Type = "manual" });
        var ctx = new TriggerContext
        {
            WorkflowId = "wf1",
            Configuration = new Dictionary<string, string>(),
            OnTriggered = _ => Task.FromResult("run1")
        };
        await source.StartAsync(ctx);
        await source.StopAsync();
        source.IsRunning.Should().BeFalse();
        await source.DisposeAsync();
    }

    [Fact]
    public void InputSchema_ReturnsConfiguredValue()
    {
        var def = new TriggerDefinition
        {
            Type = "manual",
            Configuration = new Dictionary<string, string>
            {
                ["inputSchema"] = @"{""type"":""object""}"
            }
        };
        new ManualTriggerSource(def).InputSchema.Should().Be(@"{""type"":""object""}");
    }

    [Fact]
    public void InputSchema_ReturnsNull_WhenNotConfigured()
    {
        new ManualTriggerSource(new TriggerDefinition { Type = "manual" }).InputSchema.Should().BeNull();
    }

    [Fact]
    public void Constructor_NullDefinition_Throws()
    {
        var act = () => new ManualTriggerSource(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
