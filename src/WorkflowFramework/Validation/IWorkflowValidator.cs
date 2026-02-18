namespace WorkflowFramework.Validation;

/// <summary>
/// Validates a step before execution.
/// </summary>
public interface IStepValidator
{
    /// <summary>
    /// Validates the given step with the current context.
    /// </summary>
    /// <param name="step">The step to validate.</param>
    /// <param name="context">The workflow context.</param>
    /// <returns>The validation result.</returns>
    Task<ValidationResult> ValidateAsync(IStep step, IWorkflowContext context);
}

/// <summary>
/// Validates an entire workflow before execution.
/// </summary>
public interface IWorkflowValidator
{
    /// <summary>
    /// Validates the workflow definition.
    /// </summary>
    /// <param name="workflow">The workflow to validate.</param>
    /// <returns>The validation result.</returns>
    Task<ValidationResult> ValidateAsync(IWorkflow workflow);
}

/// <summary>
/// Result of a validation operation.
/// </summary>
public sealed class ValidationResult
{
    private ValidationResult(bool isValid, IReadOnlyList<ValidationError> errors)
    {
        IsValid = isValid;
        Errors = errors;
    }

    /// <summary>
    /// Gets whether the validation passed.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new(true, Array.Empty<ValidationError>());

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public static ValidationResult Failure(params ValidationError[] errors) => new(false, errors);

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public static ValidationResult Failure(IReadOnlyList<ValidationError> errors) => new(false, errors);
}

/// <summary>
/// Represents a single validation error.
/// </summary>
public sealed class ValidationError
{
    /// <summary>
    /// Initializes a new instance of <see cref="ValidationError"/>.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="stepName">The step name (if applicable).</param>
    public ValidationError(string message, string? stepName = null)
    {
        Message = message;
        StepName = stepName;
    }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the step name this error relates to.
    /// </summary>
    public string? StepName { get; }

    /// <inheritdoc />
    public override string ToString() =>
        StepName != null ? $"[{StepName}] {Message}" : Message;
}
