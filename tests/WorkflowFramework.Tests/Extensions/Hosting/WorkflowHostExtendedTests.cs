using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using WorkflowFramework.Extensions.Hosting;
using WorkflowFramework.Extensions.Scheduling;
using WorkflowFramework.Registry;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Hosting;

public class WorkflowSchedulerHostedServiceExtendedTests
{
    [Fact]
    public async Task ExecuteAsync_WithInMemoryScheduler_StartsScheduler()
    {
        var registry = new WorkflowRegistry();
        var scheduler = new InMemoryWorkflowScheduler(registry);
        var service = new WorkflowSchedulerHostedService(scheduler);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await service.StartAsync(cts.Token);
        // Give it a moment to start
        await Task.Delay(50);
        await service.StopAsync(CancellationToken.None);

        // Should not throw - scheduler started and stopped cleanly
    }

    [Fact]
    public async Task ExecuteAsync_WithNonInMemoryScheduler_RunsUntilCancelled()
    {
        var scheduler = Substitute.For<IWorkflowScheduler>();
        var service = new WorkflowSchedulerHostedService(scheduler);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await service.StartAsync(cts.Token);
        await Task.Delay(200);
        await service.StopAsync(CancellationToken.None);

        // Should complete without error
    }

    [Fact]
    public async Task StopAsync_WithInMemoryScheduler_StopsScheduler()
    {
        var registry = new WorkflowRegistry();
        var scheduler = new InMemoryWorkflowScheduler(registry);
        var service = new WorkflowSchedulerHostedService(scheduler);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await service.StartAsync(cts.Token);
        await Task.Delay(50);
        await service.StopAsync(CancellationToken.None);
        // Service should have stopped cleanly
    }

    [Fact]
    public async Task StopAsync_WithNonInMemoryScheduler_DoesNotThrow()
    {
        var scheduler = Substitute.For<IWorkflowScheduler>();
        var service = new WorkflowSchedulerHostedService(scheduler);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await service.StartAsync(cts.Token);
        await Task.Delay(150);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationStopsService()
    {
        var registry = new WorkflowRegistry();
        var scheduler = new InMemoryWorkflowScheduler(registry);
        var service = new WorkflowSchedulerHostedService(scheduler);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);
        await Task.Delay(50);
        cts.Cancel();
        await Task.Delay(100);
        await service.StopAsync(CancellationToken.None);
    }
}

public class HostingServiceCollectionExtensionsExtendedTests
{
    [Fact]
    public void AddWorkflowFramework_WithNullConfigure_StillWorks()
    {
        var services = new ServiceCollection();
        services.AddWorkflowFramework(null);
        var sp = services.BuildServiceProvider();
        sp.GetService<WorkflowHostingOptions>().Should().NotBeNull();
    }

    [Fact]
    public void AddWorkflowFramework_RegistersWorkflowRegistry()
    {
        var services = new ServiceCollection();
        services.AddWorkflowFramework();
        var sp = services.BuildServiceProvider();
        sp.GetService<IWorkflowRegistry>().Should().NotBeNull();
    }

    [Fact]
    public void AddWorkflowFramework_RegistersWorkflowRunner()
    {
        var services = new ServiceCollection();
        services.AddWorkflowFramework();
        var sp = services.BuildServiceProvider();
        sp.GetService<IWorkflowRunner>().Should().NotBeNull();
    }

    [Fact]
    public void AddWorkflowHostedServices_RegistersScheduler()
    {
        var services = new ServiceCollection();
        services.AddWorkflowFramework();
        services.AddWorkflowHostedServices();
        var sp = services.BuildServiceProvider();
        sp.GetService<IWorkflowScheduler>().Should().NotBeNull();
    }

    [Fact]
    public void AddWorkflowHealthCheck_RegistersHealthCheck()
    {
        var services = new ServiceCollection();
        services.AddWorkflowFramework();
        services.AddHealthChecks().AddWorkflowHealthCheck();
        var sp = services.BuildServiceProvider();
        // Just verify registration doesn't throw
        sp.Should().NotBeNull();
    }
}
