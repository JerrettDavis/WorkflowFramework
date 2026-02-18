namespace WorkflowFramework;

/// <summary>
/// Represents a built workflow ready for execution.
/// </summary>
public interface IWorkflow
{
    /// <summary>
    /// Gets the name of this workflow.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the steps in this workflow.
    /// </summary>
    IReadOnlyList<IStep> Steps { get; }

    /// <summary>
    /// Executes the workflow with the given context.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <returns>A task containing the workflow result.</returns>
    Task<WorkflowResult> ExecuteAsync(IWorkflowContext context);
}

/// <summary>
/// Represents a strongly-typed built workflow.
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
public interface IWorkflow<TData> where TData : class
{
    /// <summary>
    /// Gets the name of this workflow.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the workflow with the given data.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <returns>A task containing the workflow result.</returns>
    Task<WorkflowResult<TData>> ExecuteAsync(IWorkflowContext<TData> context);
}
