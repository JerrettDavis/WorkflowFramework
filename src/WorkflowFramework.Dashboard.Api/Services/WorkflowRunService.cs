using System.Collections.Concurrent;
using WorkflowFramework.Dashboard.Api.Models;

namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// Manages workflow execution runs for the dashboard API.
/// </summary>
public sealed class WorkflowRunService
{
    private readonly IWorkflowDefinitionStore _store;
    private readonly ConcurrentDictionary<string, RunState> _runs = new();

    public WorkflowRunService(IWorkflowDefinitionStore store)
    {
        _store = store;
    }

    /// <summary>Starts a workflow run (simulated — records the run).</summary>
    public async Task<RunSummary?> StartRunAsync(string workflowId, CancellationToken ct = default)
    {
        var workflow = await _store.GetByIdAsync(workflowId, ct);
        if (workflow is null) return null;

        var run = new RunState
        {
            RunId = Guid.NewGuid().ToString("N"),
            WorkflowId = workflowId,
            WorkflowName = workflow.Definition.Name,
            Status = "Running",
            StartedAt = DateTimeOffset.UtcNow
        };
        _runs[run.RunId] = run;

        // Simulate completion after creation (in real impl this would be async execution)
        run.Status = "Completed";
        run.CompletedAt = DateTimeOffset.UtcNow;

        return ToSummary(run);
    }

    /// <summary>Lists recent runs.</summary>
    public Task<IReadOnlyList<RunSummary>> GetRunsAsync(int? limit = null, CancellationToken ct = default)
    {
        var runs = _runs.Values
            .OrderByDescending(r => r.StartedAt)
            .Take(limit ?? 100)
            .Select(ToSummary)
            .ToList();
        return Task.FromResult<IReadOnlyList<RunSummary>>(runs);
    }

    /// <summary>Gets a specific run by ID.</summary>
    public Task<RunSummary?> GetRunAsync(string runId, CancellationToken ct = default)
    {
        _runs.TryGetValue(runId, out var run);
        return Task.FromResult(run is null ? null : ToSummary(run));
    }

    /// <summary>Cancels a running workflow.</summary>
    public Task<bool> CancelRunAsync(string runId, CancellationToken ct = default)
    {
        if (!_runs.TryGetValue(runId, out var run))
            return Task.FromResult(false);

        if (run.Status == "Running")
        {
            run.Status = "Cancelled";
            run.CompletedAt = DateTimeOffset.UtcNow;
        }
        return Task.FromResult(true);
    }

    private static RunSummary ToSummary(RunState state) => new()
    {
        RunId = state.RunId,
        WorkflowId = state.WorkflowId,
        WorkflowName = state.WorkflowName,
        Status = state.Status,
        StartedAt = state.StartedAt,
        CompletedAt = state.CompletedAt
    };

    private sealed class RunState
    {
        public string RunId { get; set; } = string.Empty;
        public string WorkflowId { get; set; } = string.Empty;
        public string WorkflowName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
    }
}
