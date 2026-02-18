namespace WorkflowFramework;

/// <summary>
/// Base exception for workflow errors.
/// </summary>
public class WorkflowException : Exception
{
    /// <summary>Initializes a new instance of <see cref="WorkflowException"/>.</summary>
    public WorkflowException(string message) : base(message) { }

    /// <summary>Initializes a new instance of <see cref="WorkflowException"/>.</summary>
    public WorkflowException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when a workflow is aborted.
/// </summary>
public class WorkflowAbortedException : WorkflowException
{
    /// <summary>Initializes a new instance.</summary>
    public WorkflowAbortedException(string workflowId)
        : base($"Workflow '{workflowId}' was aborted.") { WorkflowId = workflowId; }

    /// <summary>Gets the workflow identifier.</summary>
    public string WorkflowId { get; }
}

/// <summary>
/// Exception thrown when a step execution fails.
/// </summary>
public class StepExecutionException : WorkflowException
{
    /// <summary>Initializes a new instance.</summary>
    public StepExecutionException(string stepName, Exception innerException)
        : base($"Step '{stepName}' failed: {innerException.Message}", innerException) { StepName = stepName; }

    /// <summary>Gets the step name.</summary>
    public string StepName { get; }
}
