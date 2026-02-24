namespace WorkflowFramework.Triggers.Sources;

/// <summary>
/// Trigger that fires on a cron schedule.
/// Config keys: "cronExpression" (required), "timezone" (optional, IANA or system ID).
/// </summary>
public sealed class ScheduleTriggerSource : ITriggerSource
{
    private readonly TriggerDefinition _definition;
    private Timer? _timer;
    private TriggerContext? _context;
    private CronExpression? _cron;
    private TimeZoneInfo? _timezone;
    private DateTimeOffset _lastFired = DateTimeOffset.MinValue;

    public ScheduleTriggerSource(TriggerDefinition definition)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public string Type => "schedule";
    public string DisplayName => "Schedule (Cron)";
    public bool IsRunning { get; private set; }

    public Task StartAsync(TriggerContext context, CancellationToken ct = default)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        _context = context;

        var config = context.Configuration;
        if (!config.TryGetValue("cronExpression", out var expr) || string.IsNullOrWhiteSpace(expr))
            throw new InvalidOperationException("ScheduleTriggerSource requires 'cronExpression' in configuration.");

        _cron = CronExpression.Parse(expr);

        _timezone = TimeZoneInfo.Utc;
        if (config.TryGetValue("timezone", out var tz) && !string.IsNullOrWhiteSpace(tz))
        {
            try { _timezone = TimeZoneInfo.FindSystemTimeZoneById(tz); }
            catch (TimeZoneNotFoundException) { /* fall back to UTC */ }
        }

        _timer = new Timer(OnTimerTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        IsRunning = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        IsRunning = false;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _timer?.Dispose();
        _timer = null;
        IsRunning = false;
        return default;
    }

    private async void OnTimerTick(object? state)
    {
        try
        {
            if (_cron is null || _context is null) return;

            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _timezone!);
            // Truncate to minute
            var currentMinute = new DateTimeOffset(now.Year, now.Month, now.Day,
                now.Hour, now.Minute, 0, now.Offset);

            if (currentMinute <= _lastFired) return;
            if (!_cron.Matches(currentMinute)) return;

            _lastFired = currentMinute;

            await _context.OnTriggered(new TriggerEvent
            {
                TriggerType = Type,
                Timestamp = DateTimeOffset.UtcNow,
                Payload = new Dictionary<string, object>
                {
                    ["cronExpression"] = _definition.Configuration.GetValueOrDefault("cronExpression") ?? "",
                    ["scheduledTime"] = currentMinute.ToString("O")
                }
            }).ConfigureAwait(false);
        }
        catch
        {
            // Swallow — trigger should not crash the host
        }
    }
}
