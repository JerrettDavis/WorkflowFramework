namespace WorkflowFramework;

/// <summary>
/// Represents a single step in a workflow.
/// </summary>
public interface IStep
{
    /// <summary>
    /// Gets the name of this step.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes this step with the given context.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExecuteAsync(IWorkflowContext context);
}

/// <summary>
/// Represents a strongly-typed step in a workflow.
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
public interface IStep<TData> where TData : class
{
    /// <summary>
    /// Gets the name of this step.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes this step with the given typed context.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExecuteAsync(IWorkflowContext<TData> context);
}
