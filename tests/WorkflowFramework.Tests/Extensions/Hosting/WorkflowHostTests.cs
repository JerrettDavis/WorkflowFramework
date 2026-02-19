using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WorkflowFramework.Extensions.Hosting;
using WorkflowFramework.Registry;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Hosting;

public class WorkflowHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_NullRegistry_ReturnsDegraded()
    {
        var hc = new WorkflowHealthCheck(null);
        var result = await hc.CheckHealthAsync(new HealthCheckContext());
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("not available");
    }

    [Fact]
    public async Task CheckHealthAsync_WithRegistry_ReturnsHealthy()
    {
        var registry = new WorkflowRegistry();
        var hc = new WorkflowHealthCheck(registry);
        var result = await hc.CheckHealthAsync(new HealthCheckContext());
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("registered");
    }
}

public class WorkflowHostingOptionsTests
{
    [Fact]
    public void Defaults()
    {
        var o = new WorkflowHostingOptions();
        o.MaxParallelism.Should().Be(Environment.ProcessorCount);
        o.DefaultTimeout.Should().BeNull();
    }

    [Fact]
    public void CanSetProperties()
    {
        var o = new WorkflowHostingOptions
        {
            MaxParallelism = 4,
            DefaultTimeout = TimeSpan.FromSeconds(30)
        };
        o.MaxParallelism.Should().Be(4);
        o.DefaultTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }
}

public class HostingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddWorkflowFramework_RegistersServices()
    {
        var services = new ServiceCollection();
        services.AddWorkflowFramework();
        var sp = services.BuildServiceProvider();
        sp.GetService<WorkflowHostingOptions>().Should().NotBeNull();
    }

    [Fact]
    public void AddWorkflowFramework_WithConfigure_AppliesOptions()
    {
        var services = new ServiceCollection();
        services.AddWorkflowFramework(o => o.MaxParallelism = 2);
        var sp = services.BuildServiceProvider();
        sp.GetRequiredService<WorkflowHostingOptions>().MaxParallelism.Should().Be(2);
    }

    [Fact]
    public void AddWorkflowHostedServices_RegistersScheduler()
    {
        var services = new ServiceCollection();
        services.AddWorkflowFramework();
        services.AddWorkflowHostedServices();
        // Just verify no exception - hosted service registration
        services.Should().Contain(sd => sd.ServiceType.Name.Contains("IHostedService"));
    }
}

public class WorkflowSchedulerHostedServiceTests
{
    [Fact]
    public void Constructor_NullScheduler_Throws()
    {
        FluentActions.Invoking(() => new WorkflowSchedulerHostedService(null!))
            .Should().Throw<ArgumentNullException>();
    }
}
