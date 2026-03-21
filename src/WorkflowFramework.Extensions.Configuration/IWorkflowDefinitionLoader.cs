using System.Text.Json;
using WorkflowFramework.Builder;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WorkflowFramework.Extensions.Configuration;

/// <summary>
/// Interface for loading workflow definitions from configuration files.
/// </summary>
public interface IWorkflowDefinitionLoader
{
    /// <summary>
    /// Loads a workflow definition from a string.
    /// </summary>
    /// <param name="content">The configuration content.</param>
    /// <returns>The workflow definition.</returns>
    WorkflowDefinition Load(string content);

    /// <summary>
    /// Loads a workflow definition from a file.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>The workflow definition.</returns>
    WorkflowDefinition LoadFromFile(string filePath);
}

/// <summary>
/// Registry that maps step type names to actual step types.
/// </summary>
public interface IStepRegistry
{
    /// <summary>
    /// Registers a step type by name.
    /// </summary>
    /// <param name="name">The step type name.</param>
    /// <param name="factory">Factory to create the step.</param>
    void Register(string name, Func<IStep> factory);

    /// <summary>
    /// Resolves a step by type name.
    /// </summary>
    /// <param name="name">The step type name.</param>
    /// <returns>The step instance.</returns>
    IStep Resolve(string name);

    /// <summary>
    /// Gets all registered step type names.
    /// </summary>
    IReadOnlyCollection<string> Names { get; }
}

/// <summary>
/// Default implementation of <see cref="IStepRegistry"/>.
/// </summary>
public sealed class StepRegistry : IStepRegistry
{
    private readonly Dictionary<string, Func<IStep>> _factories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a step type.
    /// </summary>
    /// <typeparam name="TStep">The step type.</typeparam>
    /// <param name="name">Optional name override (defaults to type name).</param>
    public void Register<TStep>(string? name = null) where TStep : IStep, new()
    {
        var key = name ?? typeof(TStep).Name;
        _factories[key] = () => new TStep();
    }

    /// <inheritdoc />
    public void Register(string name, Func<IStep> factory)
    {
        _factories[name] = factory;
    }

    /// <inheritdoc />
    public IStep Resolve(string name)
    {
        if (_factories.TryGetValue(name, out var factory))
            return factory();
        throw new KeyNotFoundException($"No step registered with type name '{name}'.");
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> Names => _factories.Keys;
}

/// <summary>
/// JSON implementation of <see cref="IWorkflowDefinitionLoader"/>.
/// </summary>
public sealed class JsonWorkflowDefinitionLoader : IWorkflowDefinitionLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <inheritdoc />
    public WorkflowDefinition Load(string content)
    {
        return JsonSerializer.Deserialize<WorkflowDefinition>(content, Options)
            ?? throw new InvalidOperationException("Failed to deserialize workflow definition.");
    }

    /// <inheritdoc />
    public WorkflowDefinition LoadFromFile(string filePath)
    {
        var content = File.ReadAllText(filePath);
        return Load(content);
    }
}

/// <summary>
/// YAML implementation of <see cref="IWorkflowDefinitionLoader"/>.
/// </summary>
public sealed class YamlWorkflowDefinitionLoader : IWorkflowDefinitionLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <inheritdoc />
    public WorkflowDefinition Load(string content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        return Deserializer.Deserialize<WorkflowDefinition>(content)
            ?? throw new InvalidOperationException("Failed to deserialize YAML workflow definition.");
    }

    /// <inheritdoc />
    public WorkflowDefinition LoadFromFile(string filePath)
    {
        if (filePath == null) throw new ArgumentNullException(nameof(filePath));
        var content = File.ReadAllText(filePath);
        return Load(content);
    }
}

/// <summary>
/// Builds an <see cref="IWorkflow"/> from a <see cref="WorkflowDefinition"/> and an <see cref="IStepRegistry"/>.
/// </summary>
public sealed class WorkflowDefinitionBuilder
{
    private static readonly HashSet<string> KnownCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "step", "conditional", "parallel", "foreach", "while", "dowhile",
        "retry", "try", "subworkflow", "approval", "saga"
    };

    /// <summary>Cache of exception type name → <see cref="Type"/> to avoid repeated assembly scans.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Type> ExceptionTypeCache = new();

    private readonly IStepRegistry _stepRegistry;
    private readonly IReadOnlyDictionary<string, IWorkflow>? _subWorkflows;

    /// <summary>
    /// Initializes a new instance of <see cref="WorkflowDefinitionBuilder"/>.
    /// </summary>
    /// <param name="stepRegistry">The step registry used to resolve step class names.</param>
    /// <param name="subWorkflows">Optional registry of named sub-workflows (used for <c>type: subworkflow</c>).</param>
    public WorkflowDefinitionBuilder(IStepRegistry stepRegistry, IReadOnlyDictionary<string, IWorkflow>? subWorkflows = null)
    {
        _stepRegistry = stepRegistry;
        _subWorkflows = subWorkflows;
    }

    /// <summary>
    /// Builds a workflow from a definition.
    /// </summary>
    /// <param name="definition">The workflow definition.</param>
    /// <returns>The built workflow.</returns>
    public IWorkflow Build(WorkflowDefinition definition)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));

        var builder = Workflow.Create(definition.Name);

        if (definition.Compensation)
            builder.WithCompensation();

        BuildSteps(builder, definition.Steps);

        return builder.Build();
    }

    private void BuildSteps(IWorkflowBuilder builder, List<StepDefinition> steps)
    {
        foreach (var stepDef in steps)
            BuildStep(builder, stepDef);
    }

    private void BuildStep(IWorkflowBuilder builder, StepDefinition stepDef)
    {
        var typeCategory = stepDef.Type?.ToLowerInvariant() ?? string.Empty;

        if (KnownCategories.Contains(typeCategory))
        {
            // New format: type is a composite step category
            switch (typeCategory)
            {
                case "step":
                    var className = stepDef.Class
                        ?? throw new InvalidOperationException(
                            $"Step '{stepDef.Name ?? "unnamed"}' of type 'step' requires a 'class' property.");
                    builder.Step(_stepRegistry.Resolve(className));
                    break;

                case "conditional":
                    BuildConditionalStep(builder, stepDef);
                    break;

                case "parallel":
                    BuildParallelStep(builder, stepDef);
                    break;

                case "foreach":
                    BuildForEachStep(builder, stepDef);
                    break;

                case "while":
                    BuildWhileStep(builder, stepDef);
                    break;

                case "dowhile":
                    BuildDoWhileStep(builder, stepDef);
                    break;

                case "retry":
                    BuildRetryGroupStep(builder, stepDef);
                    break;

                case "try":
                    BuildTryStep(builder, stepDef);
                    break;

                case "subworkflow":
                    BuildSubWorkflowStep(builder, stepDef);
                    break;

                case "approval":
                    BuildApprovalStep(builder, stepDef);
                    break;

                case "saga":
                    BuildSagaStep(builder, stepDef);
                    break;
            }
        }
        else if (stepDef.Parallel != null && stepDef.Parallel.Count > 0)
        {
            // Legacy format: parallel with a flat list of class names
            builder.Parallel(p =>
            {
                foreach (var parallelType in stepDef.Parallel)
                    p.Step(_stepRegistry.Resolve(parallelType));
            });
        }
        else if (stepDef.Condition != null && (stepDef.Then != null || stepDef.ThenSteps?.Count > 0))
        {
            // Legacy conditional format
            Func<IWorkflowContext, bool> predicate = ctx =>
            {
                ctx.Properties.TryGetValue(stepDef.Condition, out var val);
                return val is true or "true";
            };

            var stepName = stepDef.Name ?? "conditional";

            if (stepDef.ThenSteps?.Count > 0)
            {
                var thenStep = BuildStepsAsGroupStep($"{stepName}_then", stepDef.ThenSteps);
                if (stepDef.ElseSteps?.Count > 0)
                {
                    var elseStep = BuildStepsAsGroupStep($"{stepName}_else", stepDef.ElseSteps);
                    builder.If(predicate).Then(thenStep).Else(elseStep);
                }
                else
                {
                    builder.If(predicate).Then(thenStep).EndIf();
                }
            }
            else
            {
                var thenStep = _stepRegistry.Resolve(stepDef.Then!);
                if (stepDef.Else != null)
                {
                    var elseStep = _stepRegistry.Resolve(stepDef.Else);
                    builder.If(predicate).Then(thenStep).Else(elseStep);
                }
                else
                {
                    builder.If(predicate).Then(thenStep).EndIf();
                }
            }
        }
        else if (stepDef.Retry != null && !string.IsNullOrEmpty(stepDef.Type))
        {
            // Legacy format: single step with retry wrapping
            var retryStep = _stepRegistry.Resolve(stepDef.Type);
            builder.Retry(b => b.Step(retryStep), stepDef.Retry.MaxAttempts);
        }
        else if (!string.IsNullOrEmpty(stepDef.Class))
        {
            // New format shorthand: class without explicit category
            builder.Step(_stepRegistry.Resolve(stepDef.Class));
        }
        else if (!string.IsNullOrEmpty(stepDef.Type))
        {
            // Legacy format: type is the class name
            builder.Step(_stepRegistry.Resolve(stepDef.Type));
        }
        else
        {
            throw new InvalidOperationException(
                $"Step '{stepDef.Name ?? "unnamed"}' has no 'type' or 'class' specified.");
        }
    }

    private void BuildConditionalStep(IWorkflowBuilder builder, StepDefinition stepDef)
    {
        var conditionKey = stepDef.Condition
            ?? throw new InvalidOperationException(
                $"Conditional step '{stepDef.Name ?? "unnamed"}' requires a 'condition' property.");

        Func<IWorkflowContext, bool> predicate = ctx =>
        {
            ctx.Properties.TryGetValue(conditionKey, out var val);
            return val is true or "true";
        };

        // Prefer nested step definitions; fall back to legacy class-name strings
        var thenSteps = stepDef.ThenSteps;
        var elseSteps = stepDef.ElseSteps;
        var stepName = stepDef.Name ?? "conditional";

        IStep thenStep;
        if (thenSteps != null && thenSteps.Count > 0)
        {
            thenStep = BuildStepsAsGroupStep($"{stepName}_then", thenSteps);
        }
        else if (stepDef.Then != null)
        {
            thenStep = _stepRegistry.Resolve(stepDef.Then);
        }
        else
        {
            throw new InvalidOperationException(
                $"Conditional step '{stepDef.Name ?? "unnamed"}' requires 'then', 'thenSteps', or equivalent.");
        }

        if (elseSteps != null && elseSteps.Count > 0)
        {
            var elseStep = BuildStepsAsGroupStep($"{stepName}_else", elseSteps);
            builder.If(predicate).Then(thenStep).Else(elseStep);
        }
        else if (stepDef.Else != null)
        {
            var elseStep = _stepRegistry.Resolve(stepDef.Else);
            builder.If(predicate).Then(thenStep).Else(elseStep);
        }
        else
        {
            builder.If(predicate).Then(thenStep).EndIf();
        }
    }

    private void BuildParallelStep(IWorkflowBuilder builder, StepDefinition stepDef)
    {
        var childSteps = stepDef.Steps;
        if (childSteps == null || childSteps.Count == 0)
            throw new InvalidOperationException(
                $"Parallel step '{stepDef.Name ?? "unnamed"}' requires a non-empty 'steps' list.");

        builder.Parallel(p =>
        {
            for (var i = 0; i < childSteps.Count; i++)
            {
                var child = childSteps[i];
                // Use composite-aware grouping so nested conditionals/retries/etc. work inside parallel.
                // Include an index in the default name to ensure uniqueness when child.Name is null.
                var branchName = child.Name ?? $"branch_{i}";
                var branchStep = BuildStepsAsGroupStep(branchName, new List<StepDefinition> { child });
                p.Step(branchStep);
            }
        });
    }

    private void BuildForEachStep(IWorkflowBuilder builder, StepDefinition stepDef)
    {
        var bodySteps = stepDef.Steps ?? [];
        var itemsKey = stepDef.Condition ?? "items";

        builder.ForEach<object>(
            ctx =>
            {
                if (!ctx.Properties.TryGetValue(itemsKey, out var col)) return Array.Empty<object>();
                // Accept typed IEnumerable<object> directly.
                if (col is IEnumerable<object> typed) return typed;
                // Accept any non-string IEnumerable and project to object.
                if (col is System.Collections.IEnumerable enumerable and not string)
                    return enumerable.Cast<object>();
                return Array.Empty<object>();
            },
            b => BuildSteps(b, bodySteps));
    }

    private void BuildWhileStep(IWorkflowBuilder builder, StepDefinition stepDef)
    {
        var conditionKey = stepDef.Condition
            ?? throw new InvalidOperationException(
                $"While step '{stepDef.Name ?? "unnamed"}' requires a 'condition' property.");
        var bodySteps = stepDef.Steps ?? [];

        builder.While(
            ctx =>
            {
                ctx.Properties.TryGetValue(conditionKey, out var val);
                return val is true or "true";
            },
            b => BuildSteps(b, bodySteps));
    }

    private void BuildDoWhileStep(IWorkflowBuilder builder, StepDefinition stepDef)
    {
        var conditionKey = stepDef.Condition
            ?? throw new InvalidOperationException(
                $"DoWhile step '{stepDef.Name ?? "unnamed"}' requires a 'condition' property.");
        var bodySteps = stepDef.Steps ?? [];

        builder.DoWhile(
            b => BuildSteps(b, bodySteps),
            ctx =>
            {
                ctx.Properties.TryGetValue(conditionKey, out var val);
                return val is true or "true";
            });
    }

    private void BuildRetryGroupStep(IWorkflowBuilder builder, StepDefinition stepDef)
    {
        var bodySteps = stepDef.Steps ?? [];
        if (bodySteps.Count == 0)
            throw new InvalidOperationException(
                $"Retry step '{stepDef.Name ?? "unnamed"}' requires a non-empty 'steps' list.");

        var maxAttempts = stepDef.Retry?.MaxAttempts ?? 3;
        builder.Retry(b => BuildSteps(b, bodySteps), maxAttempts);
    }

    private void BuildTryStep(IWorkflowBuilder builder, StepDefinition stepDef)
    {
        var tryBody = stepDef.Steps ?? stepDef.ThenSteps ?? [];
        var finallyBody = stepDef.ElseSteps ?? [];
        var catchDefs = stepDef.Catch ?? [];

        var tryCatchBuilder = builder.Try(b => BuildSteps(b, tryBody));

        foreach (var catchDef in catchDefs)
        {
            var exType = ResolveExceptionType(catchDef.Exception);
            var catchSteps = catchDef.Steps.ToList();

            // Build the catch workflow once so it is not reconstructed on every exception.
            var catchBodyBuilder = Workflow.Create("catch");
            BuildSteps(catchBodyBuilder, catchSteps);
            var catchWorkflow = catchBodyBuilder.Build();

            Func<IWorkflowContext, Exception, Task> handler = (ctx, _) =>
                catchWorkflow.ExecuteAsync(ctx);

            // Use reflection to call the generic Catch<TException> with the resolved exception type.
            var catchMethod = typeof(ITryCatchBuilder)
                .GetMethod(nameof(ITryCatchBuilder.Catch))!
                .MakeGenericMethod(exType);
            tryCatchBuilder = (ITryCatchBuilder)catchMethod.Invoke(tryCatchBuilder, new object[] { handler })!;
        }

        if (finallyBody.Count > 0)
            tryCatchBuilder.Finally(b => BuildSteps(b, finallyBody));
        else
            tryCatchBuilder.EndTry();
    }

    /// <summary>
    /// Resolves an exception type by name. Tries assembly-qualified lookup, the <c>System.</c> prefix,
    /// and then scans all loaded assemblies by <c>FullName</c> and short <c>Name</c>. Falls back to
    /// <see cref="Exception"/> if the type cannot be resolved. Results are cached to avoid repeated scans.
    /// </summary>
    private static Type ResolveExceptionType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return typeof(Exception);

        return ExceptionTypeCache.GetOrAdd(typeName, static name =>
        {
            // Try assembly-qualified or fully-qualified name first.
            var resolved = Type.GetType(name)
                ?? Type.GetType($"System.{name}");

            if (resolved != null) return resolved;

            // Scan all loaded assemblies to find the type by full or short name.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Fast path: try GetType before iterating all types.
                var candidate = assembly.GetType(name);
                if (candidate != null && typeof(Exception).IsAssignableFrom(candidate))
                    return candidate;

                foreach (var type in assembly.GetTypes())
                {
                    if (!typeof(Exception).IsAssignableFrom(type)) continue;
                    if (type.FullName == name || type.Name == name)
                        return type;
                }
            }

            return typeof(Exception);
        });
    }

    private void BuildSubWorkflowStep(IWorkflowBuilder builder, StepDefinition stepDef)
    {
        var name = stepDef.SubWorkflow ?? stepDef.Class ?? stepDef.Name
            ?? throw new InvalidOperationException(
                "Sub-workflow step requires 'subWorkflow', 'class', or 'name' to identify the workflow.");

        if (_subWorkflows != null && _subWorkflows.TryGetValue(name, out var wf))
        {
            builder.SubWorkflow(wf);
        }
        else
        {
            // Fall back to treating the name as a step class registered in the registry
            builder.Step(_stepRegistry.Resolve(name));
        }
    }

    private void BuildApprovalStep(IWorkflowBuilder builder, StepDefinition stepDef)
    {
        // Try to resolve a registered approval step (using 'class' when provided); otherwise create a simple recording step.
        var stepKey = string.IsNullOrWhiteSpace(stepDef.Class) ? "approval" : stepDef.Class;
        try
        {
            var approvalStep = _stepRegistry.Resolve(stepKey);
            builder.Step(approvalStep);
        }
        catch (KeyNotFoundException)
        {
            var stepName = stepDef.Name ?? "Approval";
            var message = stepDef.Message ?? "Approval required";
            builder.Step(stepName, ctx =>
            {
                ctx.Properties[$"{stepName}.Message"] = message;
                ctx.Properties[$"{stepName}.RequiredApprovers"] = stepDef.RequiredApprovers ?? 1;
                ctx.Properties[$"{stepName}.Status"] = "Pending";
                return Task.CompletedTask;
            });
        }
    }

    private void BuildSagaStep(IWorkflowBuilder builder, StepDefinition stepDef)
    {
        var bodySteps = stepDef.Steps ?? [];
        if (bodySteps.Count == 0)
            throw new InvalidOperationException(
                $"Saga step '{stepDef.Name ?? "unnamed"}' requires a non-empty 'steps' list.");

        // Build a compensating sub-workflow for the saga group
        var sagaBuilder = Workflow.Create(stepDef.Name ?? "Saga");
        sagaBuilder.WithCompensation();
        BuildSteps(sagaBuilder, bodySteps);
        var sagaWorkflow = sagaBuilder.Build();
        builder.SubWorkflow(sagaWorkflow);
    }

    /// <summary>
    /// Resolves a single leaf <see cref="IStep"/> from a step definition.
    /// For a simple <c>type: step</c> or direct class-name reference, this returns the registry lookup.
    /// </summary>
    private IStep ResolveLeafStep(StepDefinition stepDef)
    {
        if (!string.IsNullOrEmpty(stepDef.Class))
            return _stepRegistry.Resolve(stepDef.Class);

        var typeCategory = stepDef.Type?.ToLowerInvariant() ?? string.Empty;

        if (typeCategory == "step")
        {
            var className = stepDef.Class
                ?? throw new InvalidOperationException(
                    $"Step '{stepDef.Name ?? "unnamed"}' of type 'step' requires a 'class' property.");
            return _stepRegistry.Resolve(className);
        }

        if (!string.IsNullOrEmpty(stepDef.Type) && !KnownCategories.Contains(typeCategory))
            return _stepRegistry.Resolve(stepDef.Type);

        throw new InvalidOperationException(
            $"Cannot resolve a single step from composite step definition '{stepDef.Name ?? "unnamed"}' (type='{stepDef.Type}').");
    }

    /// <summary>
    /// Builds a list of step definitions into a single <see cref="IStep"/>.
    /// Multiple steps are grouped into a sequential inline workflow step.
    /// </summary>
    private IStep BuildStepsAsGroupStep(string groupName, List<StepDefinition> steps)
    {
        if (steps.Count == 0)
            throw new InvalidOperationException($"Step group '{groupName}' is empty.");

        if (steps.Count == 1)
        {
            var singleDef = steps[0];
            var typeCategory = singleDef.Type?.ToLowerInvariant() ?? string.Empty;

            // A leaf step can be resolved directly without creating an extra inline workflow.
            // A step is a leaf if:
            //  - Type is empty/unset AND Class is set (bare class reference with no type category), OR
            //  - Type is "step" (explicit leaf category), OR
            //  - Type is set to a value that is NOT a known composite category (treated as a class name).
            // When Type IS a known composite category (e.g., subworkflow, conditional), Class is
            // interpreted as an identifier within that composite dispatch — NOT as a leaf class name.
            var isLeaf =
                (string.IsNullOrEmpty(singleDef.Type) && !string.IsNullOrEmpty(singleDef.Class)) ||
                typeCategory == "step" ||
                (!string.IsNullOrEmpty(singleDef.Type) && !KnownCategories.Contains(typeCategory));

            if (isLeaf)
                return ResolveLeafStep(singleDef);

            // Single composite step (e.g., conditional, retry, saga): wrap in an inline workflow
            // so composite dispatch logic runs correctly.
            var singleBuilder = Workflow.Create(groupName);
            BuildSteps(singleBuilder, steps);
            var singleWorkflow = singleBuilder.Build();
            return new InlineWorkflowStep(groupName, singleWorkflow);
        }

        // Multiple steps: create an inline sequential workflow
        var subBuilder = Workflow.Create(groupName);
        BuildSteps(subBuilder, steps);
        var subWorkflow = subBuilder.Build();
        return new InlineWorkflowStep(groupName, subWorkflow);
    }

    /// <summary>
    /// An inline step that executes a workflow — used when multiple steps must be grouped
    /// for a conditional Then/Else branch.
    /// </summary>
    private sealed class InlineWorkflowStep(string name, IWorkflow workflow) : IStep
    {
        public string Name => name;
        public Task ExecuteAsync(IWorkflowContext context) => workflow.ExecuteAsync(context);
    }
}
