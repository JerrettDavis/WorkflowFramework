using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WorkflowFramework.Extensions.Scheduling;
using WorkflowFramework.Registry;

namespace WorkflowFramework.Extensions.Hosting;

/// <summary>
/// Extension methods for registering WorkflowFramework hosting services.
/// </summary>
public static class HostingServiceCollectionExtensions
{
    /// <summary>
    /// Adds WorkflowFramework core services and registry.
    /// </summary>
    public static IServiceCollection AddWorkflowFramework(this IServiceCollection services, Action<WorkflowHostingOptions>? configure = null)
    {
        var options = new WorkflowHostingOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        WorkflowFramework.Extensions.DependencyInjection.ServiceCollectionExtensions.AddWorkflowFramework(services);
        services.AddSingleton<IWorkflowRegistry, WorkflowRegistry>();
        services.AddSingleton<IWorkflowRunner, WorkflowRunner>();

        return services;
    }

    /// <summary>
    /// Adds the workflow scheduler as a hosted service.
    /// </summary>
    public static IServiceCollection AddWorkflowHostedServices(this IServiceCollection services)
    {
        services.AddSingleton<IWorkflowScheduler, InMemoryWorkflowScheduler>();
        services.AddHostedService<WorkflowSchedulerHostedService>();
        return services;
    }

    /// <summary>
    /// Adds a health check for the workflow engine.
    /// </summary>
    public static IHealthChecksBuilder AddWorkflowHealthCheck(this IHealthChecksBuilder builder)
    {
        return builder.Add(new HealthCheckRegistration(
            "workflow-engine",
            sp => new WorkflowHealthCheck(sp.GetService<IWorkflowRegistry>()),
            failureStatus: HealthStatus.Degraded,
            tags: new[] { "workflow" }));
    }
}

/// <summary>
/// Options for workflow hosting configuration.
/// </summary>
public sealed class WorkflowHostingOptions
{
    /// <summary>Gets or sets the maximum parallelism for workflow execution.</summary>
    public int MaxParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>Gets or sets the default step timeout.</summary>
    public TimeSpan? DefaultTimeout { get; set; }
}
