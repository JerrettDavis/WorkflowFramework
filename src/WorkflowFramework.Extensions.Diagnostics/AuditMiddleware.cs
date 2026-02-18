namespace WorkflowFramework.Extensions.Diagnostics;

/// <summary>
/// Records an audit trail of step executions.
/// </summary>
public sealed class AuditMiddleware : IWorkflowMiddleware
{
    private readonly IAuditStore _store;

    /// <summary>
    /// Initializes a new instance of <see cref="AuditMiddleware"/>.
    /// </summary>
    /// <param name="store">The audit store.</param>
    public AuditMiddleware(IAuditStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public async Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
    {
        var entry = new AuditEntry
        {
            WorkflowId = context.WorkflowId,
            CorrelationId = context.CorrelationId,
            StepName = step.Name,
            StepIndex = context.CurrentStepIndex,
            StartedAt = DateTimeOffset.UtcNow
        };

        try
        {
            await next(context).ConfigureAwait(false);
            entry.CompletedAt = DateTimeOffset.UtcNow;
            entry.Status = AuditStatus.Completed;
        }
        catch (Exception ex)
        {
            entry.CompletedAt = DateTimeOffset.UtcNow;
            entry.Status = AuditStatus.Failed;
            entry.ErrorMessage = ex.Message;
            throw;
        }
        finally
        {
            await _store.RecordAsync(entry).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Represents an audit trail entry.
/// </summary>
public sealed class AuditEntry
{
    /// <summary>Gets or sets the workflow identifier.</summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>Gets or sets the correlation identifier.</summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>Gets or sets the step name.</summary>
    public string StepName { get; set; } = string.Empty;

    /// <summary>Gets or sets the step index.</summary>
    public int StepIndex { get; set; }

    /// <summary>Gets or sets when the step started.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Gets or sets when the step completed.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Gets or sets the step execution status.</summary>
    public AuditStatus Status { get; set; }

    /// <summary>Gets or sets the error message if the step failed.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets the duration of the step execution.</summary>
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
}

/// <summary>
/// The status of an audited step execution.
/// </summary>
public enum AuditStatus
{
    /// <summary>Step completed successfully.</summary>
    Completed,
    /// <summary>Step failed with an error.</summary>
    Failed
}

/// <summary>
/// Interface for storing audit entries.
/// </summary>
public interface IAuditStore
{
    /// <summary>
    /// Records an audit entry.
    /// </summary>
    /// <param name="entry">The audit entry.</param>
    Task RecordAsync(AuditEntry entry);

    /// <summary>
    /// Gets all audit entries for a workflow.
    /// </summary>
    /// <param name="workflowId">The workflow identifier.</param>
    /// <returns>The audit entries.</returns>
    Task<IReadOnlyList<AuditEntry>> GetEntriesAsync(string workflowId);
}

/// <summary>
/// In-memory implementation of <see cref="IAuditStore"/>.
/// </summary>
public sealed class InMemoryAuditStore : IAuditStore
{
    private readonly List<AuditEntry> _entries = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public Task RecordAsync(AuditEntry entry)
    {
        lock (_lock) _entries.Add(entry);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditEntry>> GetEntriesAsync(string workflowId)
    {
        lock (_lock)
        {
            var result = _entries.Where(e => e.WorkflowId == workflowId).ToList();
            return Task.FromResult<IReadOnlyList<AuditEntry>>(result);
        }
    }

    /// <summary>
    /// Gets all recorded audit entries.
    /// </summary>
    public IReadOnlyList<AuditEntry> AllEntries
    {
        get { lock (_lock) return _entries.ToList(); }
    }
}
