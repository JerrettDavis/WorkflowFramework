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
    private readonly IStepRegistry _stepRegistry;

    /// <summary>
    /// Initializes a new instance of <see cref="WorkflowDefinitionBuilder"/>.
    /// </summary>
    /// <param name="stepRegistry">The step registry.</param>
    public WorkflowDefinitionBuilder(IStepRegistry stepRegistry)
    {
        _stepRegistry = stepRegistry;
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
        {
            if (stepDef.Parallel != null && stepDef.Parallel.Count > 0)
            {
                builder.Parallel(p =>
                {
                    foreach (var parallelType in stepDef.Parallel)
                    {
                        p.Step(_stepRegistry.Resolve(parallelType));
                    }
                });
            }
            else if (stepDef.Condition != null && stepDef.Then != null)
            {
                // Conditional step: condition is a step type name used as a property key check
                var thenStep = _stepRegistry.Resolve(stepDef.Then);
                if (stepDef.Else != null)
                {
                    var elseStep = _stepRegistry.Resolve(stepDef.Else);
                    builder.If(ctx =>
                    {
                        ctx.Properties.TryGetValue(stepDef.Condition, out var val);
                        return val is true or "true";
                    }).Then(thenStep).Else(elseStep);
                }
                else
                {
                    builder.If(ctx =>
                    {
                        ctx.Properties.TryGetValue(stepDef.Condition, out var val);
                        return val is true or "true";
                    }).Then(thenStep).EndIf();
                }
            }
            else if (stepDef.Retry != null && !string.IsNullOrEmpty(stepDef.Type))
            {
                var retryStep = _stepRegistry.Resolve(stepDef.Type);
                builder.Retry(b => b.Step(retryStep), stepDef.Retry.MaxAttempts);
            }
            else
            {
                var step = _stepRegistry.Resolve(stepDef.Type);
                builder.Step(step);
            }
        }
    }
}
