using System.Text.Json;
using WorkflowFramework.Builder;
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
    private readonly DashboardSettingsService _settings;

    public WorkflowDefinitionCompiler(DashboardSettingsService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
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
        switch (stepDto.Type)
        {
            case "Action":
                builder.Step(stepDto.Name, async ctx =>
                {
                    var expression = stepDto.Config?.GetValueOrDefault("expression") ?? "No action defined";
                    ctx.Properties[$"{stepDto.Name}.Output"] = $"Executed: {expression}";
                    await Task.CompletedTask;
                });
                break;

            case "LlmCallStep":
                var provider = ResolveProvider(stepDto.Config);
                builder.Step(new LlmCallStep(provider, new LlmCallOptions
                {
                    StepName = stepDto.Name,
                    PromptTemplate = stepDto.Config?.GetValueOrDefault("prompt") ?? "",
                    Model = stepDto.Config?.GetValueOrDefault("model")
                }));
                break;

            case "AgentLoopStep":
                var agentProvider = ResolveProvider(stepDto.Config);
                var tools = new ToolRegistry();
                builder.Step(new AgentLoopStep(agentProvider, tools, new AgentLoopOptions
                {
                    StepName = stepDto.Name,
                    SystemPrompt = stepDto.Config?.GetValueOrDefault("systemPrompt") ?? "",
                    MaxIterations = int.TryParse(stepDto.Config?.GetValueOrDefault("maxIterations"), out var mi) ? mi : 5
                }));
                break;

            case "HttpStep":
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

            case "Conditional":
                var condExpr = stepDto.Config?.GetValueOrDefault("expression") ?? "true";
                var condBuilder = builder.If(ctx =>
                {
                    if (ctx.Properties.TryGetValue(condExpr, out var val))
                        return val?.ToString()?.ToLower() is not ("false" or "0" or "" or null);
                    return true;
                });
                var thenStep = stepDto.Then is not null
                    ? CreateStepFromDto(stepDto.Then)
                    : new NoOpStep(stepDto.Name + "_then");
                var elseStep = stepDto.Else is not null
                    ? CreateStepFromDto(stepDto.Else)
                    : new NoOpStep(stepDto.Name + "_else");
                condBuilder.Then(thenStep).Else(elseStep);
                break;

            case "Parallel":
                if (stepDto.Steps is { Count: > 0 })
                {
                    builder.Parallel(p =>
                    {
                        foreach (var child in stepDto.Steps)
                            p.Step(CreateStepFromDto(child));
                    });
                }
                break;

            case "Delay":
                var delayMs = stepDto.DelaySeconds > 0
                    ? (int)(stepDto.DelaySeconds * 1000)
                    : int.TryParse(stepDto.Config?.GetValueOrDefault("durationMs"), out var d) ? d : 1000;
                builder.Delay(TimeSpan.FromMilliseconds(delayMs));
                break;

            case "Retry":
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

            case "TryCatch":
                if (stepDto.TryBody is { Count: > 0 })
                {
                    var tryCatch = builder.Try(inner =>
                    {
                        foreach (var child in stepDto.TryBody)
                            CompileStep(inner, child);
                    })
                    .Catch<Exception>((ctx, ex) =>
                    {
                        ctx.Properties[$"{stepDto.Name}.CaughtException"] = ex.Message;
                        return Task.CompletedTask;
                    });

                    if (stepDto.FinallyBody is { Count: > 0 })
                    {
                        tryCatch.Finally(inner =>
                        {
                            foreach (var child in stepDto.FinallyBody)
                                CompileStep(inner, child);
                        });
                    }
                    else
                    {
                        tryCatch.EndTry();
                    }
                }
                break;

            case "HumanTaskStep":
                builder.Step(stepDto.Name, async ctx =>
                {
                    ctx.Properties[$"{stepDto.Name}.Status"] = "Auto-approved (dashboard mode)";
                    await Task.Delay(500, ctx.CancellationToken);
                });
                break;

            default:
                builder.Step(stepDto.Name, ctx =>
                {
                    ctx.Properties[$"{stepDto.Name}.Output"] = $"Step type '{stepDto.Type}' executed (no-op in dashboard)";
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
        var model = config?.GetValueOrDefault("model") ?? settings.DefaultModel ?? "qwen3:30b-instruct";

        return providerName.ToLower() switch
        {
            "ollama" => new OllamaAgentProvider(new OllamaOptions
            {
                BaseUrl = settings.OllamaUrl ?? "http://localhost:11434",
                DefaultModel = model
            }),
            _ => new OllamaAgentProvider(new OllamaOptions
            {
                BaseUrl = settings.OllamaUrl ?? "http://localhost:11434",
                DefaultModel = model
            })
        };
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
