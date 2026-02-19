namespace WorkflowFramework.Extensions.Diagnostics.ExecutionHistory;

/// <summary>
/// In-memory implementation of <see cref="IExecutionHistoryStore"/>.
/// Useful for testing and development.
/// </summary>
public sealed class InMemoryExecutionHistoryStore : IExecutionHistoryStore
{
    private readonly List<WorkflowRunRecord> _records = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public Task RecordRunAsync(WorkflowRunRecord record, CancellationToken cancellationToken = default)
    {
        if (record is null) throw new ArgumentNullException(nameof(record));
        lock (_lock) _records.Add(record);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowRunRecord>> GetRunsAsync(ExecutionHistoryFilter? filter = null, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            IEnumerable<WorkflowRunRecord> query = _records;

            if (filter is not null)
            {
                if (filter.WorkflowName is not null)
                    query = query.Where(r => r.WorkflowName == filter.WorkflowName);

                if (filter.Status.HasValue)
                    query = query.Where(r => r.Status == filter.Status.Value);

                if (filter.From.HasValue)
                    query = query.Where(r => r.StartedAt >= filter.From.Value);

                if (filter.To.HasValue)
                    query = query.Where(r => r.StartedAt <= filter.To.Value);

                if (filter.MaxResults.HasValue)
                    query = query.Take(filter.MaxResults.Value);
            }

            return Task.FromResult<IReadOnlyList<WorkflowRunRecord>>(query.ToList());
        }
    }

    /// <inheritdoc />
    public Task<WorkflowRunRecord?> GetRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var record = _records.FirstOrDefault(r => r.RunId == runId);
            return Task.FromResult(record);
        }
    }

    /// <summary>
    /// Gets all recorded runs. Useful for testing.
    /// </summary>
    public IReadOnlyList<WorkflowRunRecord> AllRecords
    {
        get { lock (_lock) return _records.ToList(); }
    }
}
