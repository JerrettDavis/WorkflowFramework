namespace WorkflowFramework.Validation;

/// <summary>
/// Default implementation of <see cref="IWorkflowValidator"/> that performs common validations.
/// </summary>
public sealed class DefaultWorkflowValidator : IWorkflowValidator
{
    /// <inheritdoc />
    public Task<ValidationResult> ValidateAsync(IWorkflow workflow)
    {
        if (workflow == null) throw new ArgumentNullException(nameof(workflow));

        var errors = new List<ValidationError>();

        // Validate workflow has at least one step
        if (workflow.Steps.Count == 0)
        {
            errors.Add(new ValidationError("Workflow must have at least one step."));
        }

        // Detect duplicate step names
        var stepNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in workflow.Steps)
        {
            if (!stepNames.Add(step.Name))
            {
                errors.Add(new ValidationError($"Duplicate step name '{step.Name}'.", step.Name));
            }
        }

        var result = errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);

        return Task.FromResult(result);
    }
}
