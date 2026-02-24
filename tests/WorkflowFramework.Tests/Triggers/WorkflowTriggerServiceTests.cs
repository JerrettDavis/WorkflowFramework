using FluentAssertions;
using WorkflowFramework.Triggers;
using WorkflowFramework.Triggers.Sources;
using Xunit;

namespace WorkflowFramework.Tests.Triggers;

public class WorkflowTriggerServiceTests
{
    private static TriggerSourceFactory CreateFactory() => new();

    [Fact]
    public async Task ActivateTriggersAsync_StartsManualTrigger()
    {
        var factory = CreateFactory();
        var service = new WorkflowTriggerService(factory);

        var triggers = new[]
        {
            new TriggerDefinition { Type = "manual", Enabled = true }
        };

        await service.ActivateTriggersAsync("wf1", triggers, _ => Task.FromResult("run1"));

        var counts = service.GetActiveTriggerCounts();
        counts.Should().ContainKey("wf1");
        counts["wf1"].Should().Be(1);

        await service.DisposeAsync();
    }

    [Fact]
    public async Task DeactivateTriggersAsync_RemovesTriggers()
    {
        var factory = CreateFactory();
        var service = new WorkflowTriggerService(factory);

        await service.ActivateTriggersAsync("wf1",
            new[] { new TriggerDefinition { Type = "manual", Enabled = true } },
            _ => Task.FromResult("run1"));

        await service.DeactivateTriggersAsync("wf1");

        service.GetActiveTriggerCounts().Should().NotContainKey("wf1");
        await service.DisposeAsync();
    }

    [Fact]
    public async Task DeactivateTriggersAsync_NonExistent_DoesNotThrow()
    {
        var service = new WorkflowTriggerService(CreateFactory());
        await service.DeactivateTriggersAsync("nonexistent");
        await service.DisposeAsync();
    }

    [Fact]
    public async Task ActivateTriggersAsync_DisabledTrigger_IsSkipped()
    {
        var service = new WorkflowTriggerService(CreateFactory());

        await service.ActivateTriggersAsync("wf1",
            new[] { new TriggerDefinition { Type = "manual", Enabled = false } },
            _ => Task.FromResult("run1"));

        service.GetActiveTriggerCounts().Should().NotContainKey("wf1");
        await service.DisposeAsync();
    }

    [Fact]
    public async Task ActivateTriggersAsync_MultipleTriggers()
    {
        var service = new WorkflowTriggerService(CreateFactory());

        var triggers = new[]
        {
            new TriggerDefinition { Type = "manual", Enabled = true },
            new TriggerDefinition { Type = "manual", Enabled = true }
        };

        await service.ActivateTriggersAsync("wf1", triggers, _ => Task.FromResult("run1"));
        service.GetActiveTriggerCounts()["wf1"].Should().Be(2);
        await service.DisposeAsync();
    }

    [Fact]
    public async Task ActivateTriggersAsync_ReplacesExisting()
    {
        var service = new WorkflowTriggerService(CreateFactory());

        await service.ActivateTriggersAsync("wf1",
            new[] { new TriggerDefinition { Type = "manual", Enabled = true }, new TriggerDefinition { Type = "manual", Enabled = true } },
            _ => Task.FromResult("run1"));

        await service.ActivateTriggersAsync("wf1",
            new[] { new TriggerDefinition { Type = "manual", Enabled = true } },
            _ => Task.FromResult("run1"));

        service.GetActiveTriggerCounts()["wf1"].Should().Be(1);
        await service.DisposeAsync();
    }

    [Fact]
    public async Task GetActiveSources_ReturnsCorrectSources()
    {
        var service = new WorkflowTriggerService(CreateFactory());

        await service.ActivateTriggersAsync("wf1",
            new[] { new TriggerDefinition { Type = "manual", Enabled = true } },
            _ => Task.FromResult("run1"));

        var sources = service.GetActiveSources("wf1");
        sources.Should().HaveCount(1);
        sources[0].Type.Should().Be("manual");
        await service.DisposeAsync();
    }

    [Fact]
    public async Task GetActiveSources_EmptyForUnknown()
    {
        var service = new WorkflowTriggerService(CreateFactory());
        service.GetActiveSources("nope").Should().BeEmpty();
        await service.DisposeAsync();
    }

    [Fact]
    public async Task HostedService_StopAsync_CleansUp()
    {
        var service = new WorkflowTriggerService(CreateFactory());
        await service.ActivateTriggersAsync("wf1",
            new[] { new TriggerDefinition { Type = "manual", Enabled = true } },
            _ => Task.FromResult("run1"));

        await ((Microsoft.Extensions.Hosting.IHostedService)service).StopAsync(CancellationToken.None);
        service.GetActiveTriggerCounts().Should().BeEmpty();
    }

    [Fact]
    public void Constructor_NullFactory_Throws()
    {
        var act = () => new WorkflowTriggerService(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
