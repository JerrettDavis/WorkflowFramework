namespace WorkflowFramework.Extensions.AI;

/// <summary>
/// A workflow step that calls an LLM with a prompt template.
/// </summary>
public sealed class LlmCallStep : IStep
{
    private readonly IAgentProvider _provider;
    private readonly LlmCallOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="LlmCallStep"/>.
    /// </summary>
    public LlmCallStep(IAgentProvider provider, LlmCallOptions options)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string Name => _options.StepName ?? "LlmCall";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var prompt = PromptTemplateRenderer.Render(_options.PromptTemplate, context.Properties);
        var request = new LlmRequest
        {
            Prompt = prompt,
            Variables = new Dictionary<string, object?>(context.Properties),
            Model = _options.Model,
            Temperature = _options.Temperature,
            MaxTokens = _options.MaxTokens,
            Tools = _options.Tools
        };

        var response = await _provider.CompleteAsync(request, context.CancellationToken).ConfigureAwait(false);
        context.Properties[$"{Name}.Response"] = response.Content;
        context.Properties[$"{Name}.FinishReason"] = response.FinishReason;
        if (response.Usage != null)
            context.Properties[$"{Name}.TotalTokens"] = response.Usage.TotalTokens;
        if (response.ToolCalls.Count > 0)
            context.Properties[$"{Name}.ToolCalls"] = response.ToolCalls;
    }
}

/// <summary>
/// Options for LLM call step.
/// </summary>
public sealed class LlmCallOptions
{
    /// <summary>Gets or sets the step name.</summary>
    public string? StepName { get; set; }

    /// <summary>Gets or sets the prompt template.</summary>
    public string PromptTemplate { get; set; } = string.Empty;

    /// <summary>Gets or sets the model.</summary>
    public string? Model { get; set; }

    /// <summary>Gets or sets the temperature.</summary>
    public double? Temperature { get; set; }

    /// <summary>Gets or sets the max tokens.</summary>
    public int? MaxTokens { get; set; }

    /// <summary>Gets or sets available tools.</summary>
    public IList<AgentTool> Tools { get; set; } = new List<AgentTool>();
}

/// <summary>
/// A workflow step where an AI agent makes a routing decision.
/// </summary>
public sealed class AgentDecisionStep : IStep
{
    private readonly IAgentProvider _provider;
    private readonly AgentDecisionOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="AgentDecisionStep"/>.
    /// </summary>
    public AgentDecisionStep(IAgentProvider provider, AgentDecisionOptions options)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string Name => _options.StepName ?? "AgentDecision";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var prompt = PromptTemplateRenderer.Render(_options.Prompt, context.Properties);
        var request = new AgentDecisionRequest
        {
            Prompt = prompt,
            Options = _options.Options,
            Variables = new Dictionary<string, object?>(context.Properties)
        };

        var decision = await _provider.DecideAsync(request, context.CancellationToken).ConfigureAwait(false);
        context.Properties[$"{Name}.Decision"] = decision;
    }
}

/// <summary>
/// Options for agent decision step.
/// </summary>
public sealed class AgentDecisionOptions
{
    /// <summary>Gets or sets the step name.</summary>
    public string? StepName { get; set; }

    /// <summary>Gets or sets the decision prompt.</summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>Gets or sets the available options.</summary>
    public IList<string> Options { get; set; } = new List<string>();
}

/// <summary>
/// A workflow step where an AI agent creates or modifies the workflow plan.
/// </summary>
public sealed class AgentPlanStep : IStep
{
    private readonly IAgentProvider _provider;
    private readonly AgentPlanOptions _options;

    /// <summary>
    /// Initializes a new instance of <see cref="AgentPlanStep"/>.
    /// </summary>
    public AgentPlanStep(IAgentProvider provider, AgentPlanOptions options)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public string Name => _options.StepName ?? "AgentPlan";

    /// <summary>
    /// Initializes a new instance of <see cref="AgentPlanStep"/> using the default planning options.
    /// </summary>
    public AgentPlanStep(IAgentProvider provider, string? stepName = null)
        : this(provider, new AgentPlanOptions { StepName = stepName })
    {
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var prompt = PromptTemplateRenderer.Render(_options.PromptTemplate, context.Properties);
        var request = new LlmRequest
        {
            Prompt = prompt,
            Variables = new Dictionary<string, object?>(context.Properties),
            Model = _options.Model,
            Temperature = _options.Temperature,
            MaxTokens = _options.MaxTokens
        };

        var response = await _provider.CompleteAsync(request, context.CancellationToken).ConfigureAwait(false);
        var outputKey = string.IsNullOrWhiteSpace(_options.OutputPropertyName)
            ? $"{Name}.Plan"
            : _options.OutputPropertyName!;

        context.Properties[outputKey] = response.Content;
        context.Properties[$"{Name}.FinishReason"] = response.FinishReason;
        if (response.Usage != null)
            context.Properties[$"{Name}.TotalTokens"] = response.Usage.TotalTokens;
    }
}

/// <summary>
/// Options for an agent planning step.
/// </summary>
public sealed class AgentPlanOptions
{
    /// <summary>Gets or sets the step name.</summary>
    public string? StepName { get; set; }

    /// <summary>Gets or sets the planning prompt template.</summary>
    public string PromptTemplate { get; set; } = "Given the current workflow state, suggest the next steps to take.";

    /// <summary>Gets or sets the model.</summary>
    public string? Model { get; set; }

    /// <summary>Gets or sets the temperature.</summary>
    public double? Temperature { get; set; }

    /// <summary>Gets or sets the max tokens.</summary>
    public int? MaxTokens { get; set; }

    /// <summary>Gets or sets the property name used to store the generated plan.</summary>
    public string? OutputPropertyName { get; set; }
}
