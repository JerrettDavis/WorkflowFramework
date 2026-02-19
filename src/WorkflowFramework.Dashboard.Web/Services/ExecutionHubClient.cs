using Microsoft.AspNetCore.SignalR.Client;

namespace WorkflowFramework.Dashboard.Web.Services;

/// <summary>
/// Client for the WorkflowExecutionHub SignalR hub.
/// Exposes events for real-time execution updates.
/// </summary>
public sealed class ExecutionHubClient : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private bool _started;

    public event Action<string, string>? RunStarted;
    public event Action<string, string, int>? StepStarted;
    public event Action<string, string, string, long, string?>? StepCompleted;
    public event Action<string, string, string>? StepFailed;
    public event Action<string, string, long>? RunCompleted;
    public event Action<string, string>? RunFailed;
    public event Action<string, string, string, DateTimeOffset>? LogMessage;

    public ExecutionHubClient(string hubUrl)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<string, string>("RunStarted", (runId, name) => RunStarted?.Invoke(runId, name));
        _connection.On<string, string, int>("StepStarted", (runId, step, idx) => StepStarted?.Invoke(runId, step, idx));
        _connection.On<string, string, string, long, string?>("StepCompleted", (runId, step, status, ms, output) => StepCompleted?.Invoke(runId, step, status, ms, output));
        _connection.On<string, string, string>("StepFailed", (runId, step, err) => StepFailed?.Invoke(runId, step, err));
        _connection.On<string, string, long>("RunCompleted", (runId, status, ms) => RunCompleted?.Invoke(runId, status, ms));
        _connection.On<string, string>("RunFailed", (runId, err) => RunFailed?.Invoke(runId, err));
        _connection.On<string, string, string, DateTimeOffset>("LogMessage", (runId, level, msg, ts) => LogMessage?.Invoke(runId, level, msg, ts));
    }

    public async Task StartAsync()
    {
        if (!_started)
        {
            await _connection.StartAsync();
            _started = true;
        }
    }

    public async Task SubscribeToRunAsync(string runId)
    {
        await StartAsync();
        await _connection.InvokeAsync("SubscribeToRun", runId);
    }

    public async Task UnsubscribeFromRunAsync(string runId)
    {
        await _connection.InvokeAsync("UnsubscribeFromRun", runId);
    }

    public HubConnectionState State => _connection.State;

    public async ValueTask DisposeAsync()
    {
        if (_started)
        {
            await _connection.DisposeAsync();
        }
    }
}
