using Microsoft.AspNetCore.SignalR;

namespace WorkflowFramework.Dashboard.Api.Hubs;

/// <summary>
/// SignalR hub for real-time workflow execution updates.
/// </summary>
public sealed class WorkflowExecutionHub : Hub<IWorkflowExecutionClient>
{
    /// <summary>
    /// Subscribes the caller to updates for a specific run.
    /// </summary>
    public async Task SubscribeToRun(string runId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"run-{runId}");
    }

    /// <summary>
    /// Unsubscribes the caller from updates for a specific run.
    /// </summary>
    public async Task UnsubscribeFromRun(string runId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"run-{runId}");
    }
}

/// <summary>
/// Defines client-side methods that the server can invoke.
/// </summary>
public interface IWorkflowExecutionClient
{
    Task RunStarted(string runId, string workflowName);
    Task StepStarted(string runId, string stepName, int stepIndex);
    Task StepCompleted(string runId, string stepName, string status, long durationMs, string? output);
    Task StepFailed(string runId, string stepName, string error);
    Task RunCompleted(string runId, string status, long totalDurationMs);
    Task RunFailed(string runId, string error);
    Task LogMessage(string runId, string level, string message, DateTimeOffset timestamp);
}
