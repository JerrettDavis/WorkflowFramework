using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;
using WorkflowFramework.Dashboard.Api.Hubs;

namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// Bridges WorkflowFramework's <see cref="IWorkflowEvents"/> to SignalR,
/// sending real-time execution updates to subscribed clients.
/// </summary>
public sealed class WorkflowExecutionNotifier : WorkflowEventsBase
{
    private readonly IHubContext<WorkflowExecutionHub, IWorkflowExecutionClient> _hub;
    private readonly ConcurrentDictionary<string, Stopwatch> _runTimers = new();
    private readonly ConcurrentDictionary<string, Stopwatch> _stepTimers = new();

    public WorkflowExecutionNotifier(IHubContext<WorkflowExecutionHub, IWorkflowExecutionClient> hub)
    {
        _hub = hub;
    }

    public override async Task OnWorkflowStartedAsync(IWorkflowContext context)
    {
        var runId = context.CorrelationId;
        var sw = Stopwatch.StartNew();
        _runTimers[runId] = sw;

        var workflowName = context.Properties.TryGetValue("WorkflowName", out var name)
            ? name?.ToString() ?? "Unknown"
            : "Unknown";

        await Clients(runId).RunStarted(runId, workflowName);
        await SendLog(runId, "Info", $"Workflow '{workflowName}' started");
    }

    public override async Task OnWorkflowCompletedAsync(IWorkflowContext context)
    {
        var runId = context.CorrelationId;
        var durationMs = GetAndRemoveRunTimer(runId);

        await Clients(runId).RunCompleted(runId, "Completed", durationMs);
        await SendLog(runId, "Info", $"Workflow completed in {durationMs}ms");
    }

    public override async Task OnWorkflowFailedAsync(IWorkflowContext context, Exception exception)
    {
        var runId = context.CorrelationId;
        var durationMs = GetAndRemoveRunTimer(runId);

        await Clients(runId).RunFailed(runId, exception.Message);
        await Clients(runId).RunCompleted(runId, "Failed", durationMs);
        await SendLog(runId, "Error", $"Workflow failed: {exception.Message}");
    }

    public override async Task OnStepStartedAsync(IWorkflowContext context, IStep step)
    {
        var runId = context.CorrelationId;
        var key = $"{runId}:{step.Name}";
        _stepTimers[key] = Stopwatch.StartNew();

        await Clients(runId).StepStarted(runId, step.Name, context.CurrentStepIndex);
        await SendLog(runId, "Info", $"Step '{step.Name}' started (index: {context.CurrentStepIndex})");
    }

    public override async Task OnStepCompletedAsync(IWorkflowContext context, IStep step)
    {
        var runId = context.CorrelationId;
        var durationMs = GetAndRemoveStepTimer(runId, step.Name);

        await Clients(runId).StepCompleted(runId, step.Name, "Completed", durationMs, null);
        await SendLog(runId, "Info", $"Step '{step.Name}' completed in {durationMs}ms");
    }

    public override async Task OnStepFailedAsync(IWorkflowContext context, IStep step, Exception exception)
    {
        var runId = context.CorrelationId;
        var durationMs = GetAndRemoveStepTimer(runId, step.Name);

        await Clients(runId).StepFailed(runId, step.Name, exception.Message);
        await Clients(runId).StepCompleted(runId, step.Name, "Failed", durationMs, null);
        await SendLog(runId, "Error", $"Step '{step.Name}' failed: {exception.Message}");
    }

    private IWorkflowExecutionClient Clients(string runId)
        => _hub.Clients.Group($"run-{runId}");

    private Task SendLog(string runId, string level, string message)
        => Clients(runId).LogMessage(runId, level, message, DateTimeOffset.UtcNow);

    private long GetAndRemoveRunTimer(string runId)
    {
        if (_runTimers.TryRemove(runId, out var sw))
        {
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }
        return 0;
    }

    private long GetAndRemoveStepTimer(string runId, string stepName)
    {
        var key = $"{runId}:{stepName}";
        if (_stepTimers.TryRemove(key, out var sw))
        {
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }
        return 0;
    }
}
