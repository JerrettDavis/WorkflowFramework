using System.Globalization;
using System.Reflection;
using System.Text.Json;
using WorkflowFramework.Builder;
using WorkflowFramework.Dashboard.Api.Plugins;
using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.Http;
using WorkflowFramework.Serialization;

namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// Compiles a <see cref="WorkflowDefinitionDto"/> into a runnable <see cref="IWorkflow"/>
/// using the fluent builder API.
/// </summary>
public sealed class WorkflowDefinitionCompiler
{
    private readonly IDashboardSettingsService _settings;
    private readonly PluginRegistry _pluginRegistry;
    private readonly IWorkflowDefinitionStore? _workflowStore;

    public WorkflowDefinitionCompiler(
        IDashboardSettingsService settings,
        PluginRegistry pluginRegistry,
        IWorkflowDefinitionStore? workflowStore = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _pluginRegistry = pluginRegistry ?? throw new ArgumentNullException(nameof(pluginRegistry));
        _workflowStore = workflowStore;
    }

    /// <summary>
    /// Compiles the definition into a workflow, optionally wiring an event handler.
    /// </summary>
    public IWorkflow Compile(WorkflowDefinitionDto definition, IWorkflowEvents? events = null)
    {
        var builder = Workflow.Create(definition.Name);

        if (events is not null)
            builder.WithEvents(events);

        foreach (var stepDto in definition.Steps)
            CompileStep(builder, stepDto);

        return builder.Build();
    }

    private void CompileStep(IWorkflowBuilder builder, StepDefinitionDto stepDto)
    {
        var stepType = (stepDto.Type ?? string.Empty).Trim();

        switch (stepType.ToLowerInvariant())
        {
            case "action":
                builder.Step(stepDto.Name, async ctx =>
                {
                    var expression = stepDto.Config?.GetValueOrDefault("expression") ?? "No action defined";
                    var rendered = RenderActionExpression(expression, ctx.Properties);
                    ctx.Properties[$"{stepDto.Name}.Output"] = $"Executed: {rendered}";
                    ctx.Properties[$"{stepDto.Name}.Expression"] = rendered;
                    await Task.CompletedTask;
                });
                break;

            case "llmcallstep":
                var provider = ResolveProvider(stepDto.Config);
                var llmCallOptions = new LlmCallOptions
                {
                    StepName = stepDto.Name,
                    PromptTemplate = stepDto.Config?.GetValueOrDefault("prompt") ?? "",
                    Model = stepDto.Config?.GetValueOrDefault("model")
                };
                if (double.TryParse(stepDto.Config?.GetValueOrDefault("temperature"), NumberStyles.Float, CultureInfo.InvariantCulture, out var temperature))
                    llmCallOptions.Temperature = temperature;
                if (int.TryParse(stepDto.Config?.GetValueOrDefault("maxTokens"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxTokens))
                    llmCallOptions.MaxTokens = maxTokens;
                builder.Step(new LlmCallStep(provider, llmCallOptions));
                break;

            case "agentloopstep":
                var agentProvider = ResolveProvider(stepDto.Config);
                var tools = new ToolRegistry();
                builder.Step(new AgentLoopStep(agentProvider, tools, new AgentLoopOptions
                {
                    StepName = stepDto.Name,
                    SystemPrompt = stepDto.Config?.GetValueOrDefault("systemPrompt") ?? "",
                    MaxIterations = int.TryParse(stepDto.Config?.GetValueOrDefault("maxIterations"), out var mi) ? mi : 10
                }));
                break;

            case "agentdecisionstep":
                var decisionProvider = ResolveProvider(stepDto.Config);
                builder.Step(new AgentDecisionStep(decisionProvider, new AgentDecisionOptions
                {
                    StepName = stepDto.Name,
                    Prompt = stepDto.Config?.GetValueOrDefault("prompt") ?? "",
                    Options = ParseOptions(stepDto.Config?.GetValueOrDefault("options"))
                }));
                break;

            case "agentplanstep":
                var planProvider = ResolveProvider(stepDto.Config);
                builder.Step(new AgentPlanStep(planProvider, new AgentPlanOptions
                {
                    StepName = stepDto.Name,
                    PromptTemplate = stepDto.Config?.GetValueOrDefault("objective") ?? "",
                    Model = stepDto.Config?.GetValueOrDefault("model")
                }));
                break;

            case "httpstep":
                var httpOptions = new HttpStepOptions
                {
                    Name = stepDto.Name,
                    Url = stepDto.Config?.GetValueOrDefault("url") ?? "",
                    Method = new HttpMethod(stepDto.Config?.GetValueOrDefault("method") ?? "GET"),
                    Body = stepDto.Config?.GetValueOrDefault("body"),
                    ContentType = stepDto.Config?.GetValueOrDefault("contentType") ?? "application/json",
                    EnsureSuccessStatusCode = false
                };
                if (stepDto.Config?.GetValueOrDefault("headers") is { } headersJson)
                {
                    try
                    {
                        var headerDict = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
                        if (headerDict is not null)
                            httpOptions.Headers = headerDict;
                    }
                    catch { /* ignore malformed headers */ }
                }
                builder.Step(new HttpStep(httpOptions));
                break;

            case "conditional":
                var condExpr = stepDto.Config?.GetValueOrDefault("expression") ?? "true";
                var condBuilder = builder.If(ctx =>
                {
                    if (bool.TryParse(condExpr, out var literalBool))
                        return literalBool;

                    if (double.TryParse(condExpr, NumberStyles.Float, CultureInfo.InvariantCulture, out var literalNumber))
                        return literalNumber != 0;

                    if (ctx.Properties.TryGetValue(condExpr, out var val))
                        return val?.ToString()?.ToLowerInvariant() is not ("false" or "0" or "" or null);
                    return false;
                });
                var thenStep = stepDto.Then is not null
                    ? CreateStepFromDto(stepDto.Then)
                    : new NoOpStep(stepDto.Name + "_then");
                var elseStep = stepDto.Else is not null
                    ? CreateStepFromDto(stepDto.Else)
                    : new NoOpStep(stepDto.Name + "_else");
                condBuilder.Then(thenStep).Else(elseStep);
                break;

            case "parallel":
                if (stepDto.Steps is { Count: > 0 })
                {
                    builder.Parallel(p =>
                    {
                        foreach (var child in stepDto.Steps)
                            p.Step(CreateStepFromDto(child));
                    });
                }
                break;

            case "delay":
                if (stepDto.DelaySeconds > 0 || (stepDto.DelaySeconds == 0 && !int.TryParse(stepDto.Config?.GetValueOrDefault("durationMs"), out var _)))
                {
                    builder.Delay(TimeSpan.FromSeconds(stepDto.DelaySeconds));
                }
                else if (int.TryParse(stepDto.Config?.GetValueOrDefault("durationMs"), out var delayMs))
                {
                    builder.Delay(TimeSpan.FromMilliseconds(delayMs));
                }
                else
                {
                    builder.Delay(TimeSpan.Zero);
                }
                break;

            case "timeout":
                if (stepDto.Inner is null)
                    throw new InvalidOperationException("Timeout step requires an inner step.");

                var timeoutSeconds = stepDto.TimeoutSeconds;
                if (timeoutSeconds <= 0 &&
                    !double.TryParse(stepDto.Config?.GetValueOrDefault("timeoutSeconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out timeoutSeconds))
                {
                    throw new InvalidOperationException("Timeout step requires timeoutSeconds > 0.");
                }

                if (timeoutSeconds <= 0)
                    throw new InvalidOperationException("Timeout step requires timeoutSeconds > 0.");

                builder.Step(CreateInternalFrameworkStep(
                    "WorkflowFramework.Internal.TimeoutStep",
                    CreateStepFromDto(stepDto.Inner),
                    TimeSpan.FromSeconds(timeoutSeconds)));
                break;

            case "subworkflow":
                var subWorkflowName = stepDto.SubWorkflowName ?? stepDto.Config?.GetValueOrDefault("subWorkflowName");
                builder.SubWorkflow(CreateSubWorkflow(stepDto, subWorkflowName));
                break;

            case "retry":
                if (stepDto.Steps is { Count: > 0 })
                {
                    var maxAttempts = stepDto.MaxAttempts > 0 ? stepDto.MaxAttempts : 3;
                    builder.Retry(inner =>
                    {
                        foreach (var child in stepDto.Steps)
                            CompileStep(inner, child);
                    }, maxAttempts);
                }
                break;

            case "trycatch":
                if (stepDto.TryBody is { Count: > 0 })
                {
                    var catchHandler = new Func<IWorkflowContext, Exception, Task>((ctx, ex) =>
                    {
                        ctx.Properties[$"{stepDto.Name}.CaughtException"] = ex.Message;
                        return Task.CompletedTask;
                    });

                    var catchHandlers = ResolveCatchTypes(stepDto.CatchTypes)
                        .ToDictionary(type => type, _ => catchHandler);

                    builder.Step(CreateInternalFrameworkStep(
                        "WorkflowFramework.Internal.TryCatchStep",
                        stepDto.Name,
                        stepDto.TryBody.Select(CreateStepFromDto).ToArray(),
                        catchHandlers,
                        stepDto.FinallyBody?.Select(CreateStepFromDto).ToArray()));
                }
                break;

            case "humantaskstep":
                builder.Step(stepDto.Name, async ctx =>
                {
                    ctx.Properties[$"{stepDto.Name}.Status"] = "Auto-approved (dashboard mode)";
                    await Task.Delay(500, ctx.CancellationToken);
                });
                break;

            default:
                // Check plugin registry first
                var pluginStep = _pluginRegistry.CreateStep(stepType, stepDto.Name, stepDto.Config);
                if (pluginStep is not null)
                {
                    builder.Step(pluginStep);
                    break;
                }
                builder.Step(stepDto.Name, ctx =>
                {
                    ctx.Properties[$"{stepDto.Name}.Output"] = $"Step type '{stepType}' executed (no-op in dashboard)";
                    return Task.CompletedTask;
                });
                break;
        }
    }

    private IStep CreateStepFromDto(StepDefinitionDto dto)
    {
        return new DynamicStep(dto.Name, async ctx =>
        {
            // Build a sub-workflow for this single step and execute inline
            var subBuilder = Workflow.Create(dto.Name);
            CompileStep(subBuilder, dto);
            var subWorkflow = subBuilder.Build();
            await subWorkflow.ExecuteAsync(ctx);
        });
    }

    private IAgentProvider ResolveProvider(Dictionary<string, string>? config)
    {
        var settings = _settings.Get();
        var providerName = config?.GetValueOrDefault("provider") ?? settings.DefaultProvider ?? "ollama";
        var model = config?.GetValueOrDefault("model") ?? settings.DefaultModel;

        return providerName.ToLowerInvariant() switch
        {
            "ollama" => new OllamaAgentProvider(new OllamaOptions
            {
                BaseUrl = settings.OllamaUrl ?? "http://localhost:11434",
                DefaultModel = model ?? "qwen3:30b-instruct"
            }),
            "openai" => new OpenAiAgentProvider(new OpenAiOptions
            {
                ApiKey = settings.OpenAiApiKey ?? throw new InvalidOperationException("OpenAI API key is not configured."),
                BaseUrl = settings.OpenAiBaseUrl ?? "https://api.openai.com/v1",
                DefaultModel = model ?? "gpt-4o"
            }),
            "anthropic" => new AnthropicAgentProvider(new AnthropicOptions
            {
                ApiKey = settings.AnthropicApiKey ?? throw new InvalidOperationException("Anthropic API key is not configured."),
                DefaultModel = model ?? "claude-sonnet-4-20250514"
            }),
            "huggingface" => new HuggingFaceAgentProvider(new HuggingFaceOptions
            {
                ApiKey = settings.HuggingFaceApiKey ?? throw new InvalidOperationException("Hugging Face API key is not configured."),
                DefaultModel = model ?? "mistralai/Mistral-7B-Instruct-v0.3"
            }),
            _ => throw new InvalidOperationException($"Unsupported AI provider '{providerName}'.")
        };
    }

    private static string RenderActionExpression(string expression, IDictionary<string, object?> properties)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return "No action defined";

        return PromptTemplateRenderer.Render(expression, properties);
    }

    private IWorkflow CreateSubWorkflow(StepDefinitionDto stepDto, string? subWorkflowName)
    {
        if (stepDto.Steps is { Count: > 0 })
        {
            var inlineBuilder = Workflow.Create(subWorkflowName ?? stepDto.Name);
            foreach (var child in stepDto.Steps)
                CompileStep(inlineBuilder, child);
            return inlineBuilder.Build();
        }

        if (string.IsNullOrWhiteSpace(subWorkflowName))
            throw new InvalidOperationException("SubWorkflow step requires subWorkflowName.");

        if (_workflowStore is null)
            throw new InvalidOperationException($"SubWorkflow '{subWorkflowName}' cannot be resolved because no workflow store is available.");

        var savedWorkflow = _workflowStore
            .GetAllAsync()
            .GetAwaiter()
            .GetResult()
            .FirstOrDefault(workflow =>
                string.Equals(workflow.Definition.Name, subWorkflowName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(workflow.Id, subWorkflowName, StringComparison.OrdinalIgnoreCase));

        if (savedWorkflow is null)
            throw new InvalidOperationException($"SubWorkflow '{subWorkflowName}' was not found.");

        return Compile(savedWorkflow.Definition);
    }

    private static IStep CreateInternalFrameworkStep(string typeName, params object[] args)
    {
        var type = typeof(Workflow).Assembly.GetType(typeName, throwOnError: true)!;
        var instance = Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: args,
            culture: CultureInfo.InvariantCulture);

        return instance as IStep
            ?? throw new InvalidOperationException($"Unable to create framework step '{typeName}'.");
    }

    private static IReadOnlyList<Type> ResolveCatchTypes(IReadOnlyList<string>? configuredCatchTypes)
    {
        if (configuredCatchTypes is null || configuredCatchTypes.Count == 0)
            return [typeof(Exception)];

        var resolved = new List<Type>();
        foreach (var configuredType in configuredCatchTypes.Where(static type => !string.IsNullOrWhiteSpace(type)))
        {
            var resolvedType = Type.GetType(configuredType, throwOnError: false)
                ?? AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(configuredType, throwOnError: false))
                    .FirstOrDefault(type => type is not null);

            if (resolvedType is null || !typeof(Exception).IsAssignableFrom(resolvedType))
                throw new InvalidOperationException($"Unsupported catch type '{configuredType}'.");

            resolved.Add(resolvedType);
        }

        return resolved.Count > 0 ? resolved : [typeof(Exception)];
    }

    private static IList<string> ParseOptions(string? optionsValue)
    {
        if (string.IsNullOrWhiteSpace(optionsValue))
            return [];

        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(optionsValue);
            if (parsed is { Count: > 0 })
                return parsed;
        }
        catch (JsonException)
        {
        }

        return optionsValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .ToList();
    }
}

/// <summary>
/// A step that wraps a delegate function.
/// </summary>
public sealed class DynamicStep : IStep
{
    private readonly Func<IWorkflowContext, Task> _action;

    public DynamicStep(string name, Func<IWorkflowContext, Task> action)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public string Name { get; }

    public Task ExecuteAsync(IWorkflowContext context) => _action(context);
}

/// <summary>
/// A step that does nothing.
/// </summary>
public sealed class NoOpStep : IStep
{
    public NoOpStep(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public string Name { get; }

    public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
}

/// <summary>
/// A WorkflowContext subclass that allows setting CorrelationId.
/// </summary>
public sealed class DashboardWorkflowContext : IWorkflowContext
{
    private readonly WorkflowContext _inner;

    public DashboardWorkflowContext(string correlationId, CancellationToken cancellationToken = default)
    {
        _inner = new WorkflowContext(cancellationToken);
        CorrelationId = correlationId;
    }

    public string WorkflowId => _inner.WorkflowId;
    public string CorrelationId { get; }
    public CancellationToken CancellationToken => _inner.CancellationToken;
    public IDictionary<string, object?> Properties => _inner.Properties;
    public string? CurrentStepName { get => _inner.CurrentStepName; set => _inner.CurrentStepName = value; }
    public int CurrentStepIndex { get => _inner.CurrentStepIndex; set => _inner.CurrentStepIndex = value; }
    public bool IsAborted { get => _inner.IsAborted; set => _inner.IsAborted = value; }
    public IList<WorkflowError> Errors => _inner.Errors;
}
