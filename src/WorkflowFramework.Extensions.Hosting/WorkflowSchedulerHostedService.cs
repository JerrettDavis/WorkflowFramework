using Microsoft.Extensions.Hosting;
using WorkflowFramework.Extensions.Scheduling;

namespace WorkflowFramework.Extensions.Hosting;

/// <summary>
/// Hosted service that runs the workflow scheduler.
/// </summary>
public sealed class WorkflowSchedulerHostedService : BackgroundService
{
    private readonly IWorkflowScheduler _scheduler;

    /// <summary>
    /// Initializes a new instance of <see cref="WorkflowSchedulerHostedService"/>.
    /// </summary>
    /// <param name="scheduler">The workflow scheduler.</param>
    public WorkflowSchedulerHostedService(IWorkflowScheduler scheduler)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_scheduler is InMemoryWorkflowScheduler inMemory)
        {
            await inMemory.StartAsync(stoppingToken).ConfigureAwait(false);
        }

        // Keep running until stopped
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_scheduler is InMemoryWorkflowScheduler inMemory)
        {
            await inMemory.StopAsync().ConfigureAwait(false);
        }
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
