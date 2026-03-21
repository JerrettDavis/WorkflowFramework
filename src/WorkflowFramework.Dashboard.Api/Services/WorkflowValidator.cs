using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Serialization;

namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// Validates a <see cref="WorkflowDefinitionDto"/> for correctness.
/// </summary>
public sealed class WorkflowValidator
{
    private static readonly string[] StructuredCanvasHandleKinds =
    [
        "then",
        "else",
        "body",
        "inner",
        "try",
        "finally",
        "continue"
    ];

    private static readonly HashSet<string> KnownStepTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "action", "conditional", "parallel", "forEach", "while", "doWhile",
        "retry", "timeout", "delay", "tryCatch", "saga", "subWorkflow",
        "http", "grpc", "event", "approval", "script", "transform",
        "validate", "map", "filter", "aggregate", "aiAgent", "aiCompletion",
        "log", "notification", "wait", "goto", "noop"
    };

    public ValidationResult Validate(WorkflowDefinitionDto definition)
        => Validate(definition, null, null);

    public ValidationResult Validate(WorkflowDefinitionDto definition, IReadOnlyList<SavedWorkflowDefinition>? knownWorkflows, string? currentWorkflowId = null)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(definition.Name))
            errors.Add(new ValidationError(ValidationSeverity.Error, null, "Workflow must have a name."));

        if (definition.Steps.Count == 0)
            errors.Add(new ValidationError(ValidationSeverity.Error, null, "Workflow must have at least one step."));

        ValidateCanvas(definition.Canvas, errors);

        var stepNames = new HashSet<string>();
        for (var i = 0; i < definition.Steps.Count; i++)
        {
            ValidateStep(definition.Steps[i], errors, stepNames, $"Step[{i}]");
        }

        ValidateSubWorkflowReferences(definition, errors, knownWorkflows, currentWorkflowId);

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

    private void ValidateCanvas(WorkflowCanvasDto? canvas, List<ValidationError> errors)
    {
        if (canvas is null)
            return;

        var nodes = canvas.Nodes ?? [];
        var edges = canvas.Edges ?? [];
        if (nodes.Count == 0 && edges.Count == 0)
            return;

        static bool IsSynthetic(WorkflowCanvasNodeDto node)
            => node.Config is not null
               && node.Config.TryGetValue("__syntheticKind", out var kind)
               && string.Equals(kind, "container-exit", StringComparison.Ordinal);

        static string NormalizeType(string? value)
            => value?.Trim().ToLowerInvariant() ?? string.Empty;

        static bool IsParallelBranchHandle(string value)
            => value.StartsWith("output-", StringComparison.OrdinalIgnoreCase)
               && int.TryParse(value["output-".Length..], out _);

        static string NormalizeCanvasHandleKind(WorkflowCanvasEdgeDto edge)
        {
            if (!string.IsNullOrWhiteSpace(edge.Kind))
                return edge.Kind.Trim();

            if (!string.IsNullOrWhiteSpace(edge.Label))
            {
                var label = edge.Label.Trim();
                if (StructuredCanvasHandleKinds.Contains(label, StringComparer.OrdinalIgnoreCase) || IsParallelBranchHandle(label))
                    return label;
            }

            return "output";
        }

        static bool IsSupportedCanvasHandle(WorkflowCanvasNodeDto sourceNode, string handleKind)
        {
            if (IsSynthetic(sourceNode))
                return true;

            return NormalizeType(sourceNode.Type) switch
            {
                "conditional" => handleKind is "then" or "else" or "continue",
                "trycatch" => handleKind is "try" or "finally" or "continue",
                "timeout" => handleKind is "inner" or "continue",
                "retry" or "foreach" or "while" or "dowhile" or "saga"
                    => handleKind is "body" or "continue",
                "parallel" => string.Equals(handleKind, "continue", StringComparison.OrdinalIgnoreCase) || IsParallelBranchHandle(handleKind),
                _ => string.Equals(handleKind, "output", StringComparison.OrdinalIgnoreCase)
            };
        }

        var duplicateNodeIds = nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .GroupBy(node => node.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        foreach (var duplicateNodeId in duplicateNodeIds)
            errors.Add(new ValidationError(ValidationSeverity.Error, null, $"Canvas contains duplicate node id '{duplicateNodeId}'."));

        var nodeLookup = nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .GroupBy(node => node.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var incomingCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var outgoingHandleCounts = new Dictionary<(string SourceId, string HandleKind), int>();

        foreach (var edge in edges)
        {
            var edgeName = string.IsNullOrWhiteSpace(edge.Id) ? $"{edge.Source}->{edge.Target}" : edge.Id;

            if (string.IsNullOrWhiteSpace(edge.Source))
            {
                errors.Add(new ValidationError(ValidationSeverity.Error, null, $"Canvas edge '{edgeName}' is missing a source node."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(edge.Target))
            {
                errors.Add(new ValidationError(ValidationSeverity.Error, null, $"Canvas edge '{edgeName}' is missing a target node."));
                continue;
            }

            if (!nodeLookup.TryGetValue(edge.Source, out var sourceNode))
            {
                errors.Add(new ValidationError(ValidationSeverity.Error, null, $"Canvas edge '{edgeName}' references missing source node '{edge.Source}'."));
                continue;
            }

            if (!nodeLookup.TryGetValue(edge.Target, out var targetNode))
            {
                errors.Add(new ValidationError(ValidationSeverity.Error, null, $"Canvas edge '{edgeName}' references missing target node '{edge.Target}'."));
                continue;
            }

            if (string.Equals(edge.Source, edge.Target, StringComparison.Ordinal))
            {
                errors.Add(new ValidationError(ValidationSeverity.Error, sourceNode.Label, $"Canvas edge '{edgeName}' cannot connect step '{sourceNode.Label}' to itself."));
                continue;
            }

            var handleKind = NormalizeCanvasHandleKind(edge);
            if (!IsSupportedCanvasHandle(sourceNode, handleKind))
            {
                errors.Add(new ValidationError(
                    ValidationSeverity.Error,
                    sourceNode.Label,
                    $"Canvas step '{sourceNode.Label}' uses unsupported '{handleKind}' output for step type '{sourceNode.Type}'."));
            }

            if (!IsSynthetic(targetNode))
            {
                incomingCounts[targetNode.Id] = incomingCounts.TryGetValue(targetNode.Id, out var count) ? count + 1 : 1;
            }

            if (!IsSynthetic(sourceNode))
            {
                var handleKey = (sourceNode.Id, handleKind.ToLowerInvariant());
                outgoingHandleCounts[handleKey] = outgoingHandleCounts.TryGetValue(handleKey, out var count) ? count + 1 : 1;
            }
        }

        foreach (var duplicateIncoming in incomingCounts.Where(pair => pair.Value > 1))
        {
            if (!nodeLookup.TryGetValue(duplicateIncoming.Key, out var targetNode))
                continue;

            errors.Add(new ValidationError(
                ValidationSeverity.Error,
                targetNode.Label,
                $"Canvas step '{targetNode.Label}' has multiple incoming edges. The designer only round-trips one incoming edge per step."));
        }

        foreach (var duplicateOutput in outgoingHandleCounts.Where(pair => pair.Value > 1))
        {
            if (!nodeLookup.TryGetValue(duplicateOutput.Key.SourceId, out var sourceNode))
                continue;

            errors.Add(new ValidationError(
                ValidationSeverity.Error,
                sourceNode.Label,
                $"Canvas step '{sourceNode.Label}' has multiple connections on its '{duplicateOutput.Key.HandleKind}' output. Each handle may connect to only one downstream step."));
        }
    }

    private void ValidateSubWorkflowReferences(
        WorkflowDefinitionDto definition,
        List<ValidationError> errors,
        IReadOnlyList<SavedWorkflowDefinition>? knownWorkflows,
        string? currentWorkflowId)
    {
        foreach (var (stepName, workflowName) in EnumerateSubWorkflowReferences(definition.Steps))
        {
            if (string.IsNullOrWhiteSpace(workflowName))
                continue;

            if (string.Equals(workflowName, definition.Name, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ValidationError(
                    ValidationSeverity.Error,
                    stepName,
                    $"SubWorkflow step '{stepName}' cannot reference the current workflow '{definition.Name}'."));
            }
        }

        if (knownWorkflows is null || knownWorkflows.Count == 0 || string.IsNullOrWhiteSpace(definition.Name))
            return;

        var groupedDefinitions = knownWorkflows
            .Where(workflow => !string.IsNullOrWhiteSpace(workflow.Definition?.Name))
            .GroupBy(workflow => workflow.Definition.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var definitionsByName = groupedDefinitions.ToDictionary(
            group => group.Key,
            group =>
            {
                var current = group.Value.FirstOrDefault(workflow => string.Equals(workflow.Id, currentWorkflowId, StringComparison.Ordinal));
                return current is not null ? definition : group.Value[0].Definition;
            },
            StringComparer.OrdinalIgnoreCase);
        definitionsByName[definition.Name] = definition;

        foreach (var (stepName, workflowName) in EnumerateSubWorkflowReferences(definition.Steps))
        {
            if (string.IsNullOrWhiteSpace(workflowName) || string.Equals(workflowName, definition.Name, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!groupedDefinitions.TryGetValue(workflowName, out var matches) || matches.Count == 0)
            {
                errors.Add(new ValidationError(
                    ValidationSeverity.Warning,
                    stepName,
                    $"SubWorkflow step '{stepName}' references missing workflow '{workflowName}'."));
                continue;
            }

            if (matches.Count > 1)
            {
                errors.Add(new ValidationError(
                    ValidationSeverity.Warning,
                    stepName,
                    $"SubWorkflow step '{stepName}' references workflow '{workflowName}', but multiple saved workflows share that name."));
            }
        }

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = new Stack<string>();

        void VisitWorkflow(string workflowName)
        {
            if (!definitionsByName.TryGetValue(workflowName, out var workflowDefinition))
                return;

            if (path.Contains(workflowName, StringComparer.OrdinalIgnoreCase))
            {
                var cycle = path.Reverse().Append(workflowName);
                errors.Add(new ValidationError(
                    ValidationSeverity.Error,
                    null,
                    $"SubWorkflow reference cycle detected: {string.Join(" -> ", cycle)}."));
                return;
            }

            if (!visited.Add(workflowName))
                return;

            path.Push(workflowName);
            foreach (var (_, referencedWorkflowName) in EnumerateSubWorkflowReferences(workflowDefinition.Steps))
            {
                if (!string.IsNullOrWhiteSpace(referencedWorkflowName))
                    VisitWorkflow(referencedWorkflowName);
            }

            path.Pop();
        }

        VisitWorkflow(definition.Name);
    }

    private static IEnumerable<(string StepName, string WorkflowName)> EnumerateSubWorkflowReferences(IEnumerable<StepDefinitionDto> steps)
    {
        foreach (var step in steps)
        {
            if (step.Type?.Equals("subworkflow", StringComparison.OrdinalIgnoreCase) == true
                && !string.IsNullOrWhiteSpace(step.SubWorkflowName))
            {
                yield return (step.Name, step.SubWorkflowName);
            }

            foreach (var reference in EnumerateChildSubWorkflowReferences(step))
                yield return reference;
        }
    }

    private static IEnumerable<(string StepName, string WorkflowName)> EnumerateChildSubWorkflowReferences(StepDefinitionDto step)
    {
        if (step.Steps is { Count: > 0 })
        {
            foreach (var reference in EnumerateSubWorkflowReferences(step.Steps))
                yield return reference;
        }

        if (step.Then is not null)
        {
            foreach (var reference in EnumerateSubWorkflowReferences([step.Then]))
                yield return reference;
        }

        if (step.Else is not null)
        {
            foreach (var reference in EnumerateSubWorkflowReferences([step.Else]))
                yield return reference;
        }

        if (step.Inner is not null)
        {
            foreach (var reference in EnumerateSubWorkflowReferences([step.Inner]))
                yield return reference;
        }

        if (step.TryBody is { Count: > 0 })
        {
            foreach (var reference in EnumerateSubWorkflowReferences(step.TryBody))
                yield return reference;
        }

        if (step.FinallyBody is { Count: > 0 })
        {
            foreach (var reference in EnumerateSubWorkflowReferences(step.FinallyBody))
                yield return reference;
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
