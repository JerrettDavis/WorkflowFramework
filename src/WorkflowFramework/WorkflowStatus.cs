namespace WorkflowFramework;

/// <summary>
/// Represents the status of a workflow execution.
/// </summary>
public enum WorkflowStatus
{
    /// <summary>
    /// The workflow has not started.
    /// </summary>
    Pending,

    /// <summary>
    /// The workflow is currently executing.
    /// </summary>
    Running,

    /// <summary>
    /// The workflow completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The workflow failed with errors.
    /// </summary>
    Faulted,

    /// <summary>
    /// The workflow was aborted.
    /// </summary>
    Aborted,

    /// <summary>
    /// The workflow was compensated after failure.
    /// </summary>
    Compensated,

    /// <summary>
    /// The workflow is suspended/checkpointed.
    /// </summary>
    Suspended
}
