using WorkflowFramework.Extensions.Diagnostics.ExecutionHistory;
using WorkflowFramework.Registry;

namespace WorkflowFramework.Dashboard.Services;

/// <summary>
/// Provides workflow definitions and execution history for the dashboard UI.
/// </summary>
public sealed class WorkflowDashboardService
{
    private readonly IWorkflowRegistry _registry;
    private readonly IExecutionHistoryStore _historyStore;

    /// <summary>
    /// Initializes a new instance of <see cref="WorkflowDashboardService"/>.
    /// </summary>
    public WorkflowDashboardService(IWorkflowRegistry registry, IExecutionHistoryStore historyStore)
    {
        if (registry is null) throw new ArgumentNullException(nameof(registry));
        if (historyStore is null) throw new ArgumentNullException(nameof(historyStore));

        _registry = registry;
        _historyStore = historyStore;
    }

    /// <summary>
    /// Gets summary information for all registered workflows.
    /// </summary>
    public async Task<IReadOnlyList<WorkflowSummary>> GetWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        var names = _registry.Names;
        var summaries = new List<WorkflowSummary>(names.Count);

        foreach (var name in names)
        {
            var workflow = _registry.Resolve(name);
            var runs = await _historyStore.GetRunsAsync(
                new ExecutionHistoryFilter { WorkflowName = name, MaxResults = 1 },
                cancellationToken).ConfigureAwait(false);

            var lastRun = runs.Count > 0 ? runs[0] : null;

            summaries.Add(new WorkflowSummary
            {
                Name = name,
                StepCount = workflow.Steps.Count,
                LastRunStatus = lastRun?.Status,
                LastRunAt = lastRun?.StartedAt
            });
        }

        return summaries;
    }

    /// <summary>
    /// Gets detailed information for a specific workflow.
    /// </summary>
    public WorkflowDetail GetWorkflowDetail(string workflowName)
    {
        if (workflowName is null) throw new ArgumentNullException(nameof(workflowName));

        var workflow = _registry.Resolve(workflowName);
        return new WorkflowDetail
        {
            Name = workflow.Name,
            Steps = workflow.Steps.Select(s => s.Name).ToList()
        };
    }

    /// <summary>
    /// Gets run history for a workflow, or all workflows if name is null.
    /// </summary>
    public Task<IReadOnlyList<WorkflowRunRecord>> GetRunsAsync(
        string? workflowName = null,
        int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new ExecutionHistoryFilter
        {
            WorkflowName = workflowName,
            MaxResults = maxResults
        };
        return _historyStore.GetRunsAsync(filter, cancellationToken);
    }

    /// <summary>
    /// Gets a specific run by ID.
    /// </summary>
    public Task<WorkflowRunRecord?> GetRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        if (runId is null) throw new ArgumentNullException(nameof(runId));
        return _historyStore.GetRunAsync(runId, cancellationToken);
    }

    /// <summary>
    /// Triggers a workflow run by name.
    /// </summary>
    public async Task<WorkflowResult> TriggerRunAsync(string workflowName, CancellationToken cancellationToken = default)
    {
        if (workflowName is null) throw new ArgumentNullException(nameof(workflowName));

        var workflow = _registry.Resolve(workflowName);
        var context = new WorkflowContext(cancellationToken);
        return await workflow.ExecuteAsync(context).ConfigureAwait(false);
    }
}
