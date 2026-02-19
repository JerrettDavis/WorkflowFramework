namespace WorkflowFramework.Extensions.Diagnostics.ExecutionHistory;

/// <summary>
/// Interface for storing and querying workflow execution history.
/// </summary>
public interface IExecutionHistoryStore
{
    /// <summary>
    /// Records a workflow run.
    /// </summary>
    /// <param name="record">The workflow run record.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task RecordRunAsync(WorkflowRunRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets workflow runs matching the specified filter.
    /// </summary>
    /// <param name="filter">The filter criteria.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The matching workflow run records.</returns>
    Task<IReadOnlyList<WorkflowRunRecord>> GetRunsAsync(ExecutionHistoryFilter? filter = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific workflow run by its identifier.
    /// </summary>
    /// <param name="runId">The run identifier.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The workflow run record, or null if not found.</returns>
    Task<WorkflowRunRecord?> GetRunAsync(string runId, CancellationToken cancellationToken = default);
}
