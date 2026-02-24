namespace WorkflowFramework.Triggers.Sources;

/// <summary>
/// Trigger for manual/API invocation. Does not actively listen — always "running".
/// Config keys: "inputSchema" (optional JSON schema for required input fields).
/// </summary>
public sealed class ManualTriggerSource : ITriggerSource
{
    private readonly TriggerDefinition _definition;
    private TriggerContext? _context;

    public ManualTriggerSource(TriggerDefinition definition)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public string Type => "manual";
    public string DisplayName => "Manual (API/UI)";
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Gets the input schema JSON, if configured.
    /// </summary>
    public string? InputSchema =>
        _definition.Configuration.TryGetValue("inputSchema", out var s) ? s : null;

    public Task StartAsync(TriggerContext context, CancellationToken ct = default)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        IsRunning = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        IsRunning = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Fires the trigger manually with the given payload.
    /// </summary>
    public async Task<string> FireAsync(Dictionary<string, object>? payload = null, string? correlationId = null)
    {
        if (_context is null)
            throw new InvalidOperationException("ManualTriggerSource has not been started.");

        return await _context.OnTriggered(new TriggerEvent
        {
            TriggerType = Type,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = payload ?? new Dictionary<string, object>(),
            CorrelationId = correlationId
        }).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        IsRunning = false;
        return default;
    }
}
