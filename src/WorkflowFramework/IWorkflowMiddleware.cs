namespace WorkflowFramework;

/// <summary>
/// Represents a delegate that processes a workflow step.
/// </summary>
/// <param name="context">The workflow context.</param>
/// <returns>A task representing the asynchronous operation.</returns>
public delegate Task StepDelegate(IWorkflowContext context);

/// <summary>
/// Represents middleware that wraps workflow step execution.
/// Middleware can inspect, modify, or short-circuit step execution.
/// </summary>
public interface IWorkflowMiddleware
{
    /// <summary>
    /// Invokes this middleware around the given step execution.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="step">The step being executed.</param>
    /// <param name="next">The next delegate in the pipeline.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next);
}
