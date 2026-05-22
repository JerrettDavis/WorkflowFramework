namespace WorkflowFramework.Validation;

/// <summary>
/// Default implementation of <see cref="IWorkflowValidator"/> that performs common validations.
/// Validation rules are internally composed via PatternKit <c>Specification&lt;T&gt;</c> instances
/// defined in <see cref="WorkflowSpec"/>. The public API is unchanged.
/// </summary>
public sealed class DefaultWorkflowValidator : IWorkflowValidator
{
    /// <inheritdoc />
    public Task<ValidationResult> ValidateAsync(IWorkflow workflow)
    {
        if (workflow == null) throw new ArgumentNullException(nameof(workflow));

        var errors = new List<ValidationError>();

        // Validate workflow has at least one step
        if (!WorkflowSpec.HasAtLeastOneStep.IsSatisfiedBy(workflow))
        {
            errors.Add(new ValidationError("Workflow must have at least one step."));
        }

        // Detect duplicate step names (use helper to find which names are duplicate)
        if (!WorkflowSpec.NoDuplicateStepNames.IsSatisfiedBy(workflow))
        {
            foreach (var duplicateName in WorkflowSpec.FindDuplicateStepNames(workflow))
            {
                errors.Add(new ValidationError($"Duplicate step name '{duplicateName}'.", duplicateName));
            }
        }

        var result = errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);

        return Task.FromResult(result);
    }
}
