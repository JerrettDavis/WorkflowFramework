using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkflowFramework.Triggers;
using Xunit;

namespace WorkflowFramework.Tests.Triggers;

public class TriggerServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddWorkflowTriggers_RegistersFactoryAndHostedService()
    {
        var services = new ServiceCollection();

        var returned = services.AddWorkflowTriggers();

        returned.Should().BeSameAs(services);

        await using var provider = services.BuildServiceProvider();
        var concreteFactory = provider.GetRequiredService<TriggerSourceFactory>();
        var factoryInterface = provider.GetRequiredService<ITriggerSourceFactory>();
        var workflowTriggerService = provider.GetRequiredService<WorkflowTriggerService>();
        var hostedService = provider.GetServices<IHostedService>().Single();

        factoryInterface.Should().BeSameAs(concreteFactory);
        hostedService.Should().BeSameAs(workflowTriggerService);
    }

    [Fact]
    public async Task AddWorkflowTriggers_WithConfigure_AppliesCustomFactoryRegistration()
    {
        var services = new ServiceCollection();
        var configureCalled = false;

        services.AddWorkflowTriggers(factory =>
        {
            configureCalled = true;
            factory.Register(
                "custom",
                _ => new TestTriggerSource(),
                new TriggerTypeInfo
                {
                    Type = "custom",
                    DisplayName = "Custom Trigger"
                });
        });

        await using var provider = services.BuildServiceProvider();

        configureCalled.Should().BeFalse();

        var factory = provider.GetRequiredService<TriggerSourceFactory>();
        var source = factory.Create(new TriggerDefinition { Type = "custom" });

        configureCalled.Should().BeTrue();
        source.Should().BeOfType<TestTriggerSource>();
        factory.GetAvailableTypes().Should().ContainSingle(x => x.Type == "custom" && x.DisplayName == "Custom Trigger");
    }

    private sealed class TestTriggerSource : ITriggerSource
    {
        public string Type => "custom";

        public string DisplayName => "Custom Trigger";

        public bool IsRunning { get; private set; }

        public Task StartAsync(TriggerContext context, CancellationToken ct = default)
        {
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct = default)
        {
            IsRunning = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
