namespace WorkflowFramework;

/// <summary>
/// Provides event hooks for workflow execution lifecycle.
/// </summary>
public interface IWorkflowEvents
{
    /// <summary>
    /// Called when the workflow starts.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnWorkflowStartedAsync(IWorkflowContext context);

    /// <summary>
    /// Called when the workflow completes successfully.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnWorkflowCompletedAsync(IWorkflowContext context);

    /// <summary>
    /// Called when the workflow fails.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnWorkflowFailedAsync(IWorkflowContext context, Exception exception);

    /// <summary>
    /// Called when a step starts.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="step">The step about to execute.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnStepStartedAsync(IWorkflowContext context, IStep step);

    /// <summary>
    /// Called when a step completes successfully.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="step">The step that completed.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnStepCompletedAsync(IWorkflowContext context, IStep step);

    /// <summary>
    /// Called when a step fails.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="step">The step that failed.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnStepFailedAsync(IWorkflowContext context, IStep step, Exception exception);
}

/// <summary>
/// Base implementation of <see cref="IWorkflowEvents"/> with no-op defaults.
/// </summary>
public abstract class WorkflowEventsBase : IWorkflowEvents
{
    /// <inheritdoc />
    public virtual Task OnWorkflowStartedAsync(IWorkflowContext context) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task OnWorkflowCompletedAsync(IWorkflowContext context) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task OnWorkflowFailedAsync(IWorkflowContext context, Exception exception) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task OnStepStartedAsync(IWorkflowContext context, IStep step) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task OnStepCompletedAsync(IWorkflowContext context, IStep step) => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task OnStepFailedAsync(IWorkflowContext context, IStep step, Exception exception) => Task.CompletedTask;
}
