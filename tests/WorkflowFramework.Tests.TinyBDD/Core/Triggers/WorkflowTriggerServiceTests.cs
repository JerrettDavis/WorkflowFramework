using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Triggers;
using WorkflowFramework.Triggers.Sources;
using WorkflowFramework.Tests.TinyBDD.Support;

namespace WorkflowFramework.Tests.TinyBDD.Core.Triggers;

[Feature("WorkflowTriggerService activation and deactivation")]
public class WorkflowTriggerServiceTests : TinyBddTestBase
{
    public WorkflowTriggerServiceTests(ITestOutputHelper output) : base(output) { }

    [Scenario("ActivateTriggersAsync starts enabled triggers and reports the count"), Fact]
    public async Task ActivateStartsEnabledTriggers()
    {
        var service = new WorkflowTriggerService(new TriggerSourceFactory());
        var defs = new[] { new TriggerDefinition { Type = "manual", Enabled = true } };
        await service.ActivateTriggersAsync("wf-activate", defs, _ => Task.FromResult("run-1"));
        var counts = service.GetActiveTriggerCounts();
        await service.DisposeAsync();

        await Given("active trigger counts for 'wf-activate'", () => counts)
            .Then("the count for the workflow is 1", c =>
            {
                c.Should().ContainKey("wf-activate");
                c["wf-activate"].Should().Be(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Disabled trigger definitions are not started"), Fact]
    public async Task DisabledTriggersAreSkipped()
    {
        var service = new WorkflowTriggerService(new TriggerSourceFactory());
        var defs = new[]
        {
            new TriggerDefinition { Type = "manual", Enabled = true },
            new TriggerDefinition { Type = "manual", Enabled = false }
        };
        await service.ActivateTriggersAsync("wf-disabled", defs, _ => Task.FromResult("run-id"));
        var counts = service.GetActiveTriggerCounts();
        await service.DisposeAsync();

        await Given("active trigger counts after activating one enabled and one disabled definition", () => counts)
            .Then("only one active trigger is registered", c =>
            {
                c["wf-disabled"].Should().Be(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("DeactivateTriggersAsync stops all triggers for a workflow"), Fact]
    public async Task DeactivateStopsTriggers()
    {
        var service = new WorkflowTriggerService(new TriggerSourceFactory());
        var defs = new[] { new TriggerDefinition { Type = "manual", Enabled = true } };
        await service.ActivateTriggersAsync("wf-deactivate", defs, _ => Task.FromResult("r"));
        await service.DeactivateTriggersAsync("wf-deactivate");
        var counts = service.GetActiveTriggerCounts();
        await service.DisposeAsync();

        await Given("active trigger counts after deactivation", () => counts)
            .Then("no active triggers remain for the workflow", c =>
            {
                c.Should().NotContainKey("wf-deactivate");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("IHostedService.StopAsync deactivates all workflows"), Fact]
    public async Task HostedServiceStopDeactivatesAll()
    {
        var service = new WorkflowTriggerService(new TriggerSourceFactory());
        var defs = new[] { new TriggerDefinition { Type = "manual", Enabled = true } };
        await service.ActivateTriggersAsync("wf-1", defs, _ => Task.FromResult("r"));
        await service.ActivateTriggersAsync("wf-2", defs, _ => Task.FromResult("r"));
        await ((Microsoft.Extensions.Hosting.IHostedService)service).StopAsync(default);
        var counts = service.GetActiveTriggerCounts();
        await service.DisposeAsync();

        await Given("active trigger counts after IHostedService.StopAsync", () => counts)
            .Then("no active triggers remain for any workflow", c =>
            {
                c.Should().BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ManualTriggerSource FireAsync routes to the OnTriggered callback via the service"), Fact]
    public async Task ManualFireRoutesToCallback()
    {
        var service = new WorkflowTriggerService(new TriggerSourceFactory());
        var runIds = new List<string>();
        var def = new TriggerDefinition { Type = "manual", Enabled = true };
        await service.ActivateTriggersAsync("wf-fire", new[] { def }, evt =>
        {
            var id = "run-" + evt.Timestamp.Ticks;
            runIds.Add(id);
            return Task.FromResult(id);
        });

        var sources = service.GetActiveSources("wf-fire");
        var manual = (ManualTriggerSource)sources[0];
        await manual.FireAsync();
        await service.DisposeAsync();

        await Given("runIds collected after FireAsync", () => runIds)
            .Then("the callback was invoked once", ids =>
            {
                ids.Should().ContainSingle();
                return true;
            })
            .AssertPassed();
    }
}
