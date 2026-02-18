namespace WorkflowFramework;

/// <summary>
/// Represents a step that supports compensation (saga pattern).
/// When a workflow fails, compensating steps are executed in reverse order.
/// </summary>
public interface ICompensatingStep : IStep
{
    /// <summary>
    /// Compensates (rolls back) the work done by this step.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CompensateAsync(IWorkflowContext context);
}

/// <summary>
/// Represents a strongly-typed step that supports compensation (saga pattern).
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
public interface ICompensatingStep<TData> : IStep<TData> where TData : class
{
    /// <summary>
    /// Compensates (rolls back) the work done by this step.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CompensateAsync(IWorkflowContext<TData> context);
}
