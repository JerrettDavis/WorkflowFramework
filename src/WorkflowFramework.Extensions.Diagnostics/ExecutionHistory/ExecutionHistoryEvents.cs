namespace WorkflowFramework.Extensions.Diagnostics.ExecutionHistory;

/// <summary>
/// Workflow event handler that flushes the accumulated <see cref="WorkflowRunRecord"/>
/// to the <see cref="IExecutionHistoryStore"/> when the workflow completes or fails.
/// </summary>
public sealed class ExecutionHistoryEvents : WorkflowEventsBase
{
    private readonly IExecutionHistoryStore _store;

    /// <summary>
    /// Initializes a new instance of <see cref="ExecutionHistoryEvents"/>.
    /// </summary>
    /// <param name="store">The execution history store.</param>
    public ExecutionHistoryEvents(IExecutionHistoryStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public override Task OnWorkflowStartedAsync(IWorkflowContext context)
    {
        // Capture workflow name from CurrentStepName context or WorkflowId
        // The run record is initialized by the middleware on first step
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task OnWorkflowCompletedAsync(IWorkflowContext context)
    {
        await FlushRecordAsync(context, WorkflowStatus.Completed, null).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task OnWorkflowFailedAsync(IWorkflowContext context, Exception exception)
    {
        await FlushRecordAsync(context, WorkflowStatus.Faulted, exception.Message).ConfigureAwait(false);
    }

    private async Task FlushRecordAsync(IWorkflowContext context, WorkflowStatus status, string? error)
    {
        if (!context.Properties.TryGetValue(ExecutionHistoryMiddleware.RunRecordKey, out var obj) ||
            obj is not WorkflowRunRecord record)
            return;

        record.Status = status;
        record.CompletedAt = DateTimeOffset.UtcNow;
        record.Error = error;

        await _store.RecordRunAsync(record, context.CancellationToken).ConfigureAwait(false);
    }
}
