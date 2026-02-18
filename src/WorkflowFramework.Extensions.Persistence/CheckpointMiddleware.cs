using WorkflowFramework.Persistence;

namespace WorkflowFramework.Extensions.Persistence;

/// <summary>
/// Middleware that saves workflow state after each successful step for checkpointing.
/// </summary>
public sealed class CheckpointMiddleware : IWorkflowMiddleware
{
    private readonly IWorkflowStateStore _store;

    /// <summary>
    /// Initializes a new instance of <see cref="CheckpointMiddleware"/>.
    /// </summary>
    /// <param name="store">The state store for persisting checkpoints.</param>
    public CheckpointMiddleware(IWorkflowStateStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public async Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
    {
        await next(context).ConfigureAwait(false);

        var state = new WorkflowState
        {
            WorkflowId = context.WorkflowId,
            CorrelationId = context.CorrelationId,
            LastCompletedStepIndex = context.CurrentStepIndex,
            Status = WorkflowStatus.Running,
            Properties = new Dictionary<string, object?>(context.Properties),
            Timestamp = DateTimeOffset.UtcNow
        };

        await _store.SaveCheckpointAsync(context.WorkflowId, state, context.CancellationToken)
            .ConfigureAwait(false);
    }
}
