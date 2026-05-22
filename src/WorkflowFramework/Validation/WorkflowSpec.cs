using PatternKit.Application.Specification;

namespace WorkflowFramework.Validation;

/// <summary>
/// Internal PatternKit Specification compositions for workflow validation rules.
/// Used by <see cref="DefaultWorkflowValidator"/> to express validation predicates
/// in a composable, named form while keeping the public API unchanged.
/// </summary>
internal static class WorkflowSpec
{
    /// <summary>
    /// Workflow must contain at least one step.
    /// </summary>
    public static readonly Specification<IWorkflow> HasAtLeastOneStep =
        Specification<IWorkflow>.Where(
            "HasAtLeastOneStep",
            w => w.Steps.Count > 0);

    /// <summary>
    /// Workflow must not contain duplicate step names (case-insensitive).
    /// </summary>
    public static readonly Specification<IWorkflow> NoDuplicateStepNames =
        Specification<IWorkflow>.Where(
            "NoDuplicateStepNames",
            w =>
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var step in w.Steps)
                    if (!seen.Add(step.Name))
                        return false;
                return true;
            });

    /// <summary>
    /// Combined rule: a valid workflow must satisfy both specs.
    /// </summary>
    public static readonly Specification<IWorkflow> IsValid =
        HasAtLeastOneStep.And(NoDuplicateStepNames, name: "IsValid");

    /// <summary>
    /// Returns the names of the specific duplicate step names for error messages.
    /// </summary>
    public static IEnumerable<string> FindDuplicateStepNames(IWorkflow workflow)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new List<string>();
        foreach (var step in workflow.Steps)
            if (!seen.Add(step.Name))
                duplicates.Add(step.Name);
        return duplicates;
    }
}
