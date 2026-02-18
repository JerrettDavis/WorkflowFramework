using Microsoft.Extensions.Diagnostics.HealthChecks;
using WorkflowFramework.Registry;

namespace WorkflowFramework.Extensions.Hosting;

/// <summary>
/// Health check for the workflow engine.
/// </summary>
public sealed class WorkflowHealthCheck : IHealthCheck
{
    private readonly IWorkflowRegistry? _registry;

    /// <summary>
    /// Initializes a new instance of <see cref="WorkflowHealthCheck"/>.
    /// </summary>
    /// <param name="registry">The workflow registry.</param>
    public WorkflowHealthCheck(IWorkflowRegistry? registry)
    {
        _registry = registry;
    }

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_registry == null)
            return Task.FromResult(HealthCheckResult.Degraded("Workflow registry not available."));

        var count = _registry.Names.Count;
        return Task.FromResult(HealthCheckResult.Healthy($"{count} workflow(s) registered."));
    }
}
