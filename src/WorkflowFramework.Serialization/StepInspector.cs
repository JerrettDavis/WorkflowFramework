using System.Reflection;

namespace WorkflowFramework.Serialization;

/// <summary>
/// Uses reflection to inspect internal step types and extract their structure.
/// </summary>
internal static class StepInspector
{
    public static StepDefinitionDto ToDto(IStep step)
    {
        var typeName = step.GetType().Name;

        // Handle generic types (ForEachStep`1, ConditionalStep`1, etc.)
        var baseTypeName = typeName.Contains('`')
            ? typeName[..typeName.IndexOf('`')]
            : typeName;

        return baseTypeName switch
        {
            "DelegateStep" => new StepDefinitionDto { Name = step.Name, Type = "action" },
            "ConditionalStep" => InspectConditional(step),
            "ParallelStep" => InspectParallel(step),
            "ForEachStep" => InspectWithBody(step, "forEach"),
            "WhileStep" => InspectWithBody(step, "while"),
            "DoWhileStep" => InspectWithBody(step, "doWhile"),
            "RetryGroupStep" => InspectRetry(step),
            "TimeoutStep" => InspectTimeout(step),
            "TryCatchStep" => InspectTryCatch(step),
            "SubWorkflowStep" => InspectSubWorkflow(step),
            "DelayStep" => InspectDelay(step),
            _ => InspectCustom(step)
        };
    }

    private static StepDefinitionDto InspectConditional(IStep step)
    {
        var dto = new StepDefinitionDto { Name = step.Name, Type = "conditional" };
        var type = step.GetType();

        var thenField = GetField(type, "_thenStep") ?? GetField(type, "thenStep");
        if (thenField?.GetValue(step) is IStep thenStep)
            dto.Then = ToDto(thenStep);

        var elseField = GetField(type, "elseStep");
        if (elseField?.GetValue(step) is IStep elseStep)
            dto.Else = ToDto(elseStep);

        return dto;
    }

    private static StepDefinitionDto InspectParallel(IStep step)
    {
        var dto = new StepDefinitionDto { Name = step.Name, Type = "parallel" };
        var stepsField = GetField(step.GetType(), "_steps");
        if (stepsField?.GetValue(step) is IReadOnlyList<IStep> steps)
            dto.Steps = steps.Select(ToDto).ToList();
        return dto;
    }

    private static StepDefinitionDto InspectWithBody(IStep step, string typeName)
    {
        var dto = new StepDefinitionDto { Name = step.Name, Type = typeName };
        var bodyField = GetField(step.GetType(), "body");
        if (bodyField?.GetValue(step) is IStep[] body)
            dto.Steps = body.Select(ToDto).ToList();
        return dto;
    }

    private static StepDefinitionDto InspectRetry(IStep step)
    {
        var dto = new StepDefinitionDto { Name = step.Name, Type = "retry" };
        var type = step.GetType();

        var bodyField = GetField(type, "body");
        if (bodyField?.GetValue(step) is IStep[] body)
            dto.Steps = body.Select(ToDto).ToList();

        var maxField = GetField(type, "maxAttempts");
        if (maxField?.GetValue(step) is int max)
            dto.MaxAttempts = max;

        return dto;
    }

    private static StepDefinitionDto InspectTimeout(IStep step)
    {
        var dto = new StepDefinitionDto { Name = step.Name, Type = "timeout" };
        var type = step.GetType();

        var innerField = GetField(type, "inner");
        if (innerField?.GetValue(step) is IStep inner)
            dto.Inner = ToDto(inner);

        var timeoutField = GetField(type, "timeout");
        if (timeoutField?.GetValue(step) is TimeSpan ts)
            dto.TimeoutSeconds = ts.TotalSeconds;

        return dto;
    }

    private static StepDefinitionDto InspectTryCatch(IStep step)
    {
        var dto = new StepDefinitionDto { Name = step.Name, Type = "tryCatch" };
        var type = step.GetType();

        var tryField = GetField(type, "tryBody");
        if (tryField?.GetValue(step) is IStep[] tryBody)
            dto.TryBody = tryBody.Select(ToDto).ToList();

        var catchField = GetField(type, "catchHandlers");
        if (catchField?.GetValue(step) is System.Collections.IDictionary dict)
            dto.CatchTypes = dict.Keys.Cast<Type>().Select(t => t.FullName ?? t.Name).ToList();

        var finallyField = GetField(type, "finallyBody");
        if (finallyField?.GetValue(step) is IStep[] finallyBody)
            dto.FinallyBody = finallyBody.Select(ToDto).ToList();

        return dto;
    }

    private static StepDefinitionDto InspectSubWorkflow(IStep step)
    {
        var dto = new StepDefinitionDto { Name = step.Name, Type = "subWorkflow" };
        var subField = GetField(step.GetType(), "subWorkflow");
        if (subField?.GetValue(step) is IWorkflow sub)
        {
            dto.SubWorkflowName = sub.Name;
            dto.Steps = sub.Steps.Select(ToDto).ToList();
        }
        return dto;
    }

    private static StepDefinitionDto InspectDelay(IStep step)
    {
        var dto = new StepDefinitionDto { Name = step.Name, Type = "delay" };
        var field = GetField(step.GetType(), "delay");
        if (field?.GetValue(step) is TimeSpan ts)
            dto.DelaySeconds = ts.TotalSeconds;
        return dto;
    }

    private static StepDefinitionDto InspectCustom(IStep step)
    {
        var dto = new StepDefinitionDto
        {
            Name = step.Name,
            Type = step.GetType().FullName ?? step.GetType().Name
        };

        // If it implements ICompensatingStep, mark it as saga
        if (step is ICompensatingStep)
            dto.Type = "saga:" + dto.Type;

        return dto;
    }

    private static FieldInfo? GetField(Type type, string name)
    {
        // Search through the type hierarchy for fields (including private/backing fields from primary constructors)
        var current = type;
        while (current != null)
        {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) return field;

            // Primary constructor parameters become fields like <name>P
            field = current.GetField($"<{name}>P", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) return field;

            current = current.BaseType;
        }
        return null;
    }
}
