namespace WorkflowFramework;

/// <summary>
/// Represents the context that flows through a workflow, carrying state between steps.
/// </summary>
public interface IWorkflowContext
{
    /// <summary>
    /// Gets the unique identifier for this workflow execution.
    /// </summary>
    string WorkflowId { get; }

    /// <summary>
    /// Gets the correlation identifier for tracking across distributed systems.
    /// </summary>
    string CorrelationId { get; }

    /// <summary>
    /// Gets the cancellation token for this workflow execution.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the properties bag for storing arbitrary data during workflow execution.
    /// </summary>
    IDictionary<string, object?> Properties { get; }

    /// <summary>
    /// Gets or sets the name of the currently executing step.
    /// </summary>
    string? CurrentStepName { get; set; }

    /// <summary>
    /// Gets the index of the currently executing step.
    /// </summary>
    int CurrentStepIndex { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the workflow has been aborted.
    /// </summary>
    bool IsAborted { get; set; }

    /// <summary>
    /// Gets the list of errors that occurred during workflow execution.
    /// </summary>
    IList<WorkflowError> Errors { get; }
}

/// <summary>
/// Represents a strongly-typed workflow context with custom data.
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
public interface IWorkflowContext<TData> : IWorkflowContext
    where TData : class
{
    /// <summary>
    /// Gets or sets the strongly-typed data flowing through the workflow.
    /// </summary>
    TData Data { get; set; }
}
