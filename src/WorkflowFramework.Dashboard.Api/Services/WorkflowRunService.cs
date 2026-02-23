using System.Collections.Concurrent;
using WorkflowFramework.Dashboard.Api.Models;

namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// Manages workflow execution runs for the dashboard API.
/// </summary>
public sealed class WorkflowRunService
{
    private readonly IWorkflowDefinitionStore _store;
    private readonly WorkflowDefinitionCompiler _compiler;
    private readonly WorkflowExecutionNotifier _notifier;
    private readonly ConcurrentDictionary<string, RunState> _runs = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellations = new();

    public WorkflowRunService(
        IWorkflowDefinitionStore store,
        WorkflowDefinitionCompiler compiler,
        WorkflowExecutionNotifier notifier)
    {
        _store = store;
        _compiler = compiler;
        _notifier = notifier;
    }

    /// <summary>Starts a workflow run with real async execution.</summary>
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

        var cts = new CancellationTokenSource();
        _cancellations[run.RunId] = cts;

        // Fire-and-forget execution (tracked by run state)
        _ = ExecuteWorkflowAsync(run, workflow.Definition, cts.Token);

        return ToSummary(run);
    }

    private async Task ExecuteWorkflowAsync(
        Serialization.WorkflowDefinitionDto definition,
        RunState run,
        CancellationToken ct)
        => await ExecuteWorkflowAsync(run, definition, ct);

    private async Task ExecuteWorkflowAsync(
        RunState run,
        Serialization.WorkflowDefinitionDto definition,
        CancellationToken ct)
    {
        try
        {
            var compiledWorkflow = _compiler.Compile(definition, _notifier);
            var context = new DashboardWorkflowContext(run.RunId, ct);
            context.Properties["WorkflowName"] = definition.Name;

            var result = await compiledWorkflow.ExecuteAsync(context);

            run.Status = result.Status == WorkflowStatus.Completed ? "Completed" : "Failed";
            run.CompletedAt = DateTimeOffset.UtcNow;
            run.StepResults = context.Properties
                .Where(p => p.Key.Contains('.'))
                .ToDictionary(p => p.Key, p => p.Value?.ToString() ?? "");
            if (result.Errors.Count > 0)
                run.Error = string.Join("; ", result.Errors.Select(e => $"[{e.StepName}] {e.Exception.Message}"));
        }
        catch (OperationCanceledException)
        {
            run.Status = "Cancelled";
            run.CompletedAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            run.Status = "Failed";
            run.Error = ex.Message;
            run.CompletedAt = DateTimeOffset.UtcNow;
        }
        finally
        {
            _cancellations.TryRemove(run.RunId, out _);
        }
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
            if (_cancellations.TryRemove(runId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
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
        CompletedAt = state.CompletedAt,
        StepResults = state.StepResults,
        Error = state.Error
    };

    private sealed class RunState
    {
        public string RunId { get; set; } = string.Empty;
        public string WorkflowId { get; set; } = string.Empty;
        public string WorkflowName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public Dictionary<string, string>? StepResults { get; set; }
        public string? Error { get; set; }
    }
}
