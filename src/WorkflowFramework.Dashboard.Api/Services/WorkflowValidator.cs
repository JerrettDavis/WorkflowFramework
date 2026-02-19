using WorkflowFramework.Serialization;

namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// Validates a <see cref="WorkflowDefinitionDto"/> for correctness.
/// </summary>
public sealed class WorkflowValidator
{
    private static readonly HashSet<string> KnownStepTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "action", "conditional", "parallel", "forEach", "while", "doWhile",
        "retry", "timeout", "delay", "tryCatch", "saga", "subWorkflow",
        "http", "grpc", "event", "approval", "script", "transform",
        "validate", "map", "filter", "aggregate", "aiAgent", "aiCompletion",
        "log", "notification", "wait", "goto", "noop"
    };

    public ValidationResult Validate(WorkflowDefinitionDto definition)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(definition.Name))
            errors.Add(new ValidationError(ValidationSeverity.Error, null, "Workflow must have a name."));

        if (definition.Steps.Count == 0)
            errors.Add(new ValidationError(ValidationSeverity.Error, null, "Workflow must have at least one step."));

        var stepNames = new HashSet<string>();
        for (var i = 0; i < definition.Steps.Count; i++)
        {
            ValidateStep(definition.Steps[i], errors, stepNames, $"Step[{i}]");
        }

        // DAG cycle detection for non-loop steps (based on child step references)
        ValidateNoCycles(definition.Steps, errors);

        return new ValidationResult(errors);
    }

    private void ValidateStep(StepDefinitionDto step, List<ValidationError> errors, HashSet<string> names, string path)
    {
        if (string.IsNullOrWhiteSpace(step.Name))
            errors.Add(new ValidationError(ValidationSeverity.Error, path, "Step must have a name."));
        else if (!names.Add(step.Name))
            errors.Add(new ValidationError(ValidationSeverity.Error, step.Name, $"Duplicate step name: '{step.Name}'."));

        if (string.IsNullOrWhiteSpace(step.Type))
            errors.Add(new ValidationError(ValidationSeverity.Error, step.Name, "Step must have a type."));
        else if (!KnownStepTypes.Contains(step.Type))
            errors.Add(new ValidationError(ValidationSeverity.Warning, step.Name, $"Unknown step type: '{step.Type}'."));

        var type = step.Type?.ToLowerInvariant();

        if (type == "conditional")
        {
            if (step.Then is null)
                errors.Add(new ValidationError(ValidationSeverity.Error, step.Name, "Conditional step must have a 'then' branch."));
            if (step.Else is null)
                errors.Add(new ValidationError(ValidationSeverity.Warning, step.Name, "Conditional step has no 'else' branch."));

            if (step.Then is not null) ValidateStep(step.Then, errors, names, $"{step.Name}.Then");
            if (step.Else is not null) ValidateStep(step.Else, errors, names, $"{step.Name}.Else");
        }

        if (type == "retry")
        {
            if (step.MaxAttempts <= 0)
                errors.Add(new ValidationError(ValidationSeverity.Error, step.Name, "Retry step must have maxAttempts > 0."));
        }

        if (type == "timeout")
        {
            if (step.TimeoutSeconds <= 0)
                errors.Add(new ValidationError(ValidationSeverity.Error, step.Name, "Timeout step must have timeoutSeconds > 0."));
            if (step.Inner is null)
                errors.Add(new ValidationError(ValidationSeverity.Error, step.Name, "Timeout step must have an inner step."));
            if (step.Inner is not null)
                ValidateStep(step.Inner, errors, names, $"{step.Name}.Inner");
        }

        if (type == "delay")
        {
            if (step.DelaySeconds <= 0)
                errors.Add(new ValidationError(ValidationSeverity.Warning, step.Name, "Delay step has delaySeconds <= 0."));
        }

        if (type is "parallel" or "foreach" or "while" or "dowhile" or "saga" or "subworkflow")
        {
            if (step.Steps is null or { Count: 0 })
                errors.Add(new ValidationError(ValidationSeverity.Warning, step.Name, $"{step.Type} step has no child steps."));
            else
            {
                for (var i = 0; i < step.Steps.Count; i++)
                    ValidateStep(step.Steps[i], errors, names, $"{step.Name}.Steps[{i}]");
            }
        }

        if (type == "trycatch")
        {
            if (step.TryBody is null or { Count: 0 })
                errors.Add(new ValidationError(ValidationSeverity.Error, step.Name, "TryCatch step must have a try body."));
            else
            {
                for (var i = 0; i < step.TryBody.Count; i++)
                    ValidateStep(step.TryBody[i], errors, names, $"{step.Name}.TryBody[{i}]");
            }
            if (step.FinallyBody is not null)
            {
                for (var i = 0; i < step.FinallyBody.Count; i++)
                    ValidateStep(step.FinallyBody[i], errors, names, $"{step.Name}.FinallyBody[{i}]");
            }
        }
    }

    private void ValidateNoCycles(List<StepDefinitionDto> steps, List<ValidationError> errors)
    {
        // Build adjacency from sequential steps (step[i] → step[i+1]) 
        // and check for duplicate references that could form cycles.
        // For the visual designer, cycles are mainly a concern with goto/subWorkflow references.
        var visited = new HashSet<string>();
        var inStack = new HashSet<string>();

        var adjacency = new Dictionary<string, List<string>>();
        for (var i = 0; i < steps.Count; i++)
        {
            var name = steps[i].Name ?? $"__step_{i}";
            adjacency.TryAdd(name, new List<string>());
            if (i + 1 < steps.Count)
            {
                var next = steps[i + 1].Name ?? $"__step_{i + 1}";
                adjacency[name].Add(next);
            }
            // subWorkflow references
            if (steps[i].Type?.Equals("subworkflow", StringComparison.OrdinalIgnoreCase) == true
                && !string.IsNullOrEmpty(steps[i].SubWorkflowName))
            {
                adjacency[name].Add(steps[i].SubWorkflowName!);
            }
        }

        foreach (var node in adjacency.Keys)
        {
            if (!visited.Contains(node) && HasCycleDfs(node, adjacency, visited, inStack))
            {
                errors.Add(new ValidationError(ValidationSeverity.Error, null, "Workflow contains a cycle (excluding loop steps)."));
                break;
            }
        }
    }

    private static bool HasCycleDfs(string node, Dictionary<string, List<string>> adj,
        HashSet<string> visited, HashSet<string> inStack)
    {
        visited.Add(node);
        inStack.Add(node);

        if (adj.TryGetValue(node, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                if (inStack.Contains(neighbor)) return true;
                if (!visited.Contains(neighbor) && HasCycleDfs(neighbor, adj, visited, inStack)) return true;
            }
        }

        inStack.Remove(node);
        return false;
    }
}

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

public sealed record ValidationError(ValidationSeverity Severity, string? StepName, string Message);

public sealed class ValidationResult
{
    public List<ValidationError> Errors { get; }
    public bool IsValid => !Errors.Any(e => e.Severity == ValidationSeverity.Error);
    public int ErrorCount => Errors.Count(e => e.Severity == ValidationSeverity.Error);
    public int WarningCount => Errors.Count(e => e.Severity == ValidationSeverity.Warning);

    public ValidationResult(List<ValidationError> errors)
    {
        Errors = errors;
    }
}
