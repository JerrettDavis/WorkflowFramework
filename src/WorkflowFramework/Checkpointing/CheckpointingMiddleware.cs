namespace WorkflowFramework.Checkpointing;

/// <summary>
/// Middleware that saves a checkpoint after each successful step execution.
/// </summary>
public sealed class CheckpointingMiddleware : IWorkflowMiddleware
{
    private readonly IWorkflowCheckpointStore _store;

    /// <summary>
    /// Initializes a new instance of <see cref="CheckpointingMiddleware"/>.
    /// </summary>
    public CheckpointingMiddleware(IWorkflowCheckpointStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <inheritdoc />
    public async Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
    {
        await next(context).ConfigureAwait(false);

        // Save checkpoint after successful step execution
        await _store.SaveAsync(
            context.WorkflowId,
            context.CurrentStepIndex,
            context.Properties,
            context.CancellationToken).ConfigureAwait(false);
    }
}
