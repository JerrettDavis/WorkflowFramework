using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// Background service that checks every 60 seconds for workflows with cron schedules and executes them.
/// </summary>
public sealed class WorkflowSchedulerService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<WorkflowSchedulerService> _logger;
    private readonly ConcurrentDictionary<string, ScheduleEntry> _schedules = new();

    public WorkflowSchedulerService(IServiceProvider services, ILogger<WorkflowSchedulerService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public void SetSchedule(string workflowId, string cronExpression, bool enabled)
    {
        if (!SimpleCronParser.IsValid(cronExpression))
            throw new ArgumentException($"Invalid cron expression: {cronExpression}");

        _schedules[workflowId] = new ScheduleEntry
        {
            WorkflowId = workflowId,
            CronExpression = cronExpression,
            Enabled = enabled
        };
    }

    public void RemoveSchedule(string workflowId)
    {
        _schedules.TryRemove(workflowId, out _);
    }

    public ScheduleEntry? GetSchedule(string workflowId)
    {
        _schedules.TryGetValue(workflowId, out var entry);
        return entry;
    }

    public IReadOnlyList<ScheduleEntry> GetAllSchedules() => _schedules.Values.ToList();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                // Truncate to minute
                var truncated = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Offset);

                foreach (var entry in _schedules.Values)
                {
                    if (!entry.Enabled) continue;
                    if (!SimpleCronParser.Matches(entry.CronExpression, truncated)) continue;
                    if (entry.LastRun.HasValue && (truncated - entry.LastRun.Value).TotalSeconds < 60) continue;

                    entry.LastRun = truncated;
                    _ = TriggerWorkflowAsync(entry.WorkflowId, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in workflow scheduler");
            }

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }

    private async Task TriggerWorkflowAsync(string workflowId, CancellationToken ct)
    {
        try
        {
            var runService = _services.GetRequiredService<WorkflowRunService>();
            var run = await runService.StartRunAsync(workflowId, ct);
            if (run is not null)
                _logger.LogInformation("Scheduled run started for workflow {WorkflowId}: {RunId}", workflowId, run.RunId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start scheduled run for workflow {WorkflowId}", workflowId);
        }
    }

    public sealed class ScheduleEntry
    {
        public string WorkflowId { get; set; } = "";
        public string CronExpression { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public DateTimeOffset? LastRun { get; set; }
    }
}
