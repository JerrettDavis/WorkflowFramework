namespace WorkflowFramework;

/// <summary>
/// Represents the result of a workflow execution.
/// </summary>
public class WorkflowResult
{
    /// <summary>
    /// Initializes a new instance of <see cref="WorkflowResult"/>.
    /// </summary>
    /// <param name="status">The workflow status.</param>
    /// <param name="context">The workflow context at completion.</param>
    public WorkflowResult(WorkflowStatus status, IWorkflowContext context)
    {
        Status = status;
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Gets the final status of the workflow.
    /// </summary>
    public WorkflowStatus Status { get; }

    /// <summary>
    /// Gets the workflow context at completion.
    /// </summary>
    public IWorkflowContext Context { get; }

    /// <summary>
    /// Gets a value indicating whether the workflow completed successfully.
    /// </summary>
    public bool IsSuccess => Status == WorkflowStatus.Completed;

    /// <summary>
    /// Gets the errors from the workflow context.
    /// </summary>
    public IReadOnlyList<WorkflowError> Errors => (IReadOnlyList<WorkflowError>)Context.Errors;
}

/// <summary>
/// Represents a strongly-typed workflow result.
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
public class WorkflowResult<TData> : WorkflowResult where TData : class
{
    /// <summary>
    /// Initializes a new instance of <see cref="WorkflowResult{TData}"/>.
    /// </summary>
    /// <param name="status">The workflow status.</param>
    /// <param name="context">The typed workflow context at completion.</param>
    public WorkflowResult(WorkflowStatus status, IWorkflowContext<TData> context)
        : base(status, context)
    {
        TypedContext = context;
    }

    /// <summary>
    /// Gets the strongly-typed context.
    /// </summary>
    public IWorkflowContext<TData> TypedContext { get; }

    /// <summary>
    /// Gets the workflow data.
    /// </summary>
    public TData Data => TypedContext.Data;
}
