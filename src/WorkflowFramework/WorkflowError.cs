namespace WorkflowFramework;

/// <summary>
/// Represents an error that occurred during workflow execution.
/// </summary>
public sealed class WorkflowError
{
    /// <summary>
    /// Initializes a new instance of <see cref="WorkflowError"/>.
    /// </summary>
    /// <param name="stepName">The name of the step where the error occurred.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="timestamp">The time the error occurred.</param>
    public WorkflowError(string stepName, Exception exception, DateTimeOffset timestamp)
    {
        StepName = stepName ?? throw new ArgumentNullException(nameof(stepName));
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the name of the step where the error occurred.
    /// </summary>
    public string StepName { get; }

    /// <summary>
    /// Gets the exception that was thrown.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets the time the error occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; }
}
