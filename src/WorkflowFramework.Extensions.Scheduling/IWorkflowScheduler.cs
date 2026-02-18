namespace WorkflowFramework.Extensions.Scheduling;

/// <summary>
/// Scheduler for executing workflows at specified times or intervals.
/// </summary>
public interface IWorkflowScheduler : IDisposable
{
    /// <summary>
    /// Schedules a workflow for future execution.
    /// </summary>
    /// <param name="workflowName">The workflow name.</param>
    /// <param name="executeAt">When to execute.</param>
    /// <param name="context">The workflow context.</param>
    /// <returns>A schedule identifier.</returns>
    Task<string> ScheduleAsync(string workflowName, DateTimeOffset executeAt, IWorkflowContext context);

    /// <summary>
    /// Schedules a workflow with a cron expression.
    /// </summary>
    /// <param name="workflowName">The workflow name.</param>
    /// <param name="cronExpression">The cron expression (5-part: min hour dom month dow).</param>
    /// <param name="contextFactory">Factory to create context for each execution.</param>
    /// <returns>A schedule identifier.</returns>
    Task<string> ScheduleCronAsync(string workflowName, string cronExpression, Func<IWorkflowContext> contextFactory);

    /// <summary>
    /// Cancels a scheduled workflow.
    /// </summary>
    /// <param name="scheduleId">The schedule identifier.</param>
    /// <returns>True if the schedule was cancelled.</returns>
    Task<bool> CancelAsync(string scheduleId);

    /// <summary>
    /// Gets all pending schedules.
    /// </summary>
    /// <returns>The pending schedules.</returns>
    Task<IReadOnlyList<ScheduledWorkflow>> GetPendingAsync();
}

/// <summary>
/// Represents a scheduled workflow execution.
/// </summary>
public sealed class ScheduledWorkflow
{
    /// <summary>
    /// Gets or sets the schedule identifier.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the workflow name.
    /// </summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the workflow should execute.
    /// </summary>
    public DateTimeOffset? ExecuteAt { get; set; }

    /// <summary>
    /// Gets or sets the cron expression (for recurring schedules).
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Gets or sets whether this schedule is recurring.
    /// </summary>
    public bool IsRecurring { get; set; }
}

/// <summary>
/// Simple cron expression parser supporting 5-part expressions (minute hour day-of-month month day-of-week).
/// </summary>
public static class CronParser
{
    /// <summary>
    /// Gets the next occurrence of the cron expression after the given time.
    /// </summary>
    /// <param name="cronExpression">The cron expression.</param>
    /// <param name="after">The reference time.</param>
    /// <returns>The next occurrence, or null if invalid.</returns>
    public static DateTimeOffset? GetNextOccurrence(string cronExpression, DateTimeOffset after)
    {
        var parts = cronExpression.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5)
            throw new FormatException("Cron expression must have exactly 5 parts: minute hour day-of-month month day-of-week");

        var minutes = ParseField(parts[0], 0, 59);
        var hours = ParseField(parts[1], 0, 23);
        var daysOfMonth = ParseField(parts[2], 1, 31);
        var months = ParseField(parts[3], 1, 12);
        var daysOfWeek = ParseField(parts[4], 0, 6);

        var candidate = after.AddMinutes(1);
        candidate = new DateTimeOffset(candidate.Year, candidate.Month, candidate.Day,
            candidate.Hour, candidate.Minute, 0, candidate.Offset);

        // Search up to 2 years ahead
        var limit = after.AddYears(2);
        while (candidate < limit)
        {
            if (months.Contains(candidate.Month) &&
                daysOfMonth.Contains(candidate.Day) &&
                daysOfWeek.Contains((int)candidate.DayOfWeek) &&
                hours.Contains(candidate.Hour) &&
                minutes.Contains(candidate.Minute))
            {
                return candidate;
            }

            candidate = candidate.AddMinutes(1);
        }

        return null;
    }

    private static HashSet<int> ParseField(string field, int min, int max)
    {
        var result = new HashSet<int>();
        if (field == "*")
        {
            for (var i = min; i <= max; i++) result.Add(i);
            return result;
        }

        foreach (var part in field.Split(','))
        {
            if (part.Contains('/'))
            {
                var slashParts = part.Split('/');
                var start = slashParts[0] == "*" ? min : int.Parse(slashParts[0]);
                var interval = int.Parse(slashParts[1]);
                for (var i = start; i <= max; i += interval) result.Add(i);
            }
            else if (part.Contains('-'))
            {
                var rangeParts = part.Split('-');
                var from = int.Parse(rangeParts[0]);
                var to = int.Parse(rangeParts[1]);
                for (var i = from; i <= to; i++) result.Add(i);
            }
            else
            {
                result.Add(int.Parse(part));
            }
        }

        return result;
    }
}

/// <summary>
/// In-memory implementation of <see cref="IWorkflowScheduler"/>.
/// </summary>
public sealed class InMemoryWorkflowScheduler : IWorkflowScheduler
{
    private readonly Registry.IWorkflowRegistry _registry;
    private readonly List<ScheduleEntry> _entries = new();
    private readonly object _lock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="InMemoryWorkflowScheduler"/>.
    /// </summary>
    /// <param name="registry">The workflow registry.</param>
    public InMemoryWorkflowScheduler(Registry.IWorkflowRegistry registry)
    {
        _registry = registry;
    }

    /// <inheritdoc />
    public Task<string> ScheduleAsync(string workflowName, DateTimeOffset executeAt, IWorkflowContext context)
    {
        var entry = new ScheduleEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            WorkflowName = workflowName,
            ExecuteAt = executeAt,
            Context = context
        };
        lock (_lock) _entries.Add(entry);
        return Task.FromResult(entry.Id);
    }

    /// <inheritdoc />
    public Task<string> ScheduleCronAsync(string workflowName, string cronExpression, Func<IWorkflowContext> contextFactory)
    {
        var next = CronParser.GetNextOccurrence(cronExpression, DateTimeOffset.UtcNow);
        var entry = new ScheduleEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            WorkflowName = workflowName,
            ExecuteAt = next,
            CronExpression = cronExpression,
            ContextFactory = contextFactory,
            IsRecurring = true
        };
        lock (_lock) _entries.Add(entry);
        return Task.FromResult(entry.Id);
    }

    /// <inheritdoc />
    public Task<bool> CancelAsync(string scheduleId)
    {
        lock (_lock)
        {
            var idx = _entries.FindIndex(e => e.Id == scheduleId);
            if (idx >= 0) { _entries.RemoveAt(idx); return Task.FromResult(true); }
        }
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ScheduledWorkflow>> GetPendingAsync()
    {
        lock (_lock)
        {
            var result = _entries.Select(e => new ScheduledWorkflow
            {
                Id = e.Id,
                WorkflowName = e.WorkflowName,
                ExecuteAt = e.ExecuteAt,
                CronExpression = e.CronExpression,
                IsRecurring = e.IsRecurring
            }).ToList();
            return Task.FromResult<IReadOnlyList<ScheduledWorkflow>>(result);
        }
    }

    /// <summary>
    /// Starts the scheduler background pump that checks for due schedules.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(InMemoryWorkflowScheduler));
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _pumpTask = PumpAsync(_cts.Token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the scheduler background pump.
    /// </summary>
    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_pumpTask != null)
        {
            try { await _pumpTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }
    }

    /// <summary>
    /// Manually ticks the scheduler, executing any due workflows. Useful for testing.
    /// </summary>
    public async Task TickAsync()
    {
        List<ScheduleEntry> dueEntries;
        lock (_lock)
        {
            dueEntries = _entries
                .Where(e => e.ExecuteAt.HasValue && e.ExecuteAt.Value <= DateTimeOffset.UtcNow)
                .ToList();
        }

        foreach (var entry in dueEntries)
        {
            try
            {
                var workflow = _registry.Resolve(entry.WorkflowName);
                var context = entry.Context ?? entry.ContextFactory?.Invoke() ?? new WorkflowContext();
                await workflow.ExecuteAsync(context).ConfigureAwait(false);
                ExecutedCount++;
            }
            catch
            {
                // Swallow execution errors in scheduler
            }

            lock (_lock)
            {
                if (entry.IsRecurring && entry.CronExpression != null)
                {
                    var next = CronParser.GetNextOccurrence(entry.CronExpression, DateTimeOffset.UtcNow);
                    if (next.HasValue)
                    {
                        entry.ExecuteAt = next.Value;
                        entry.Context = entry.ContextFactory?.Invoke();
                    }
                    else
                    {
                        _entries.Remove(entry);
                    }
                }
                else
                {
                    _entries.Remove(entry);
                }
            }
        }
    }

    /// <summary>
    /// Gets the number of workflows executed by the scheduler. Useful for testing.
    /// </summary>
    public int ExecutedCount { get; private set; }

    private CancellationTokenSource? _cts;
    private Task? _pumpTask;

    private async Task PumpAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await TickAsync().ConfigureAwait(false);
            try { await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private sealed class ScheduleEntry
    {
        public string Id { get; set; } = string.Empty;
        public string WorkflowName { get; set; } = string.Empty;
        public DateTimeOffset? ExecuteAt { get; set; }
        public string? CronExpression { get; set; }
        public IWorkflowContext? Context { get; set; }
        public Func<IWorkflowContext>? ContextFactory { get; set; }
        public bool IsRecurring { get; set; }
    }
}

/// <summary>
/// Approval step configuration.
/// </summary>
public sealed class ApprovalConfig
{
    /// <summary>
    /// Gets or sets the approval name/description.
    /// </summary>
    public string Name { get; set; } = "Approval";

    /// <summary>
    /// Gets or sets the timeout for the approval.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Gets or sets the escalation timeout.
    /// </summary>
    public TimeSpan? EscalationTimeout { get; set; }
}

/// <summary>
/// Service for handling workflow approvals.
/// </summary>
public interface IApprovalService
{
    /// <summary>
    /// Requests approval and waits for a response.
    /// </summary>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <param name="config">The approval configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if approved, false if rejected.</returns>
    Task<bool> RequestApprovalAsync(string workflowId, ApprovalConfig config, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory approval service for testing.
/// </summary>
public sealed class InMemoryApprovalService : IApprovalService
{
    private readonly Dictionary<string, TaskCompletionSource<bool>> _pending = new();

    /// <inheritdoc />
    public Task<bool> RequestApprovalAsync(string workflowId, ApprovalConfig config, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<bool>();
        _pending[workflowId] = tcs;
        cancellationToken.Register(() => tcs.TrySetCanceled());

        if (config.Timeout.HasValue)
        {
            _ = Task.Delay(config.Timeout.Value, cancellationToken).ContinueWith(_ =>
                tcs.TrySetResult(false), TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        return tcs.Task;
    }

    /// <summary>
    /// Approves a pending workflow approval.
    /// </summary>
    /// <param name="workflowId">The workflow identifier.</param>
    public void Approve(string workflowId)
    {
        if (_pending.TryGetValue(workflowId, out var tcs))
        {
            tcs.TrySetResult(true);
            _pending.Remove(workflowId);
        }
    }

    /// <summary>
    /// Rejects a pending workflow approval.
    /// </summary>
    /// <param name="workflowId">The workflow identifier.</param>
    public void Reject(string workflowId)
    {
        if (_pending.TryGetValue(workflowId, out var tcs))
        {
            tcs.TrySetResult(false);
            _pending.Remove(workflowId);
        }
    }
}
