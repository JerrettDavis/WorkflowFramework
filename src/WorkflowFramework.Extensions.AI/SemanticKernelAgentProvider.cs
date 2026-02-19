#if NET8_0_OR_GREATER
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
namespace WorkflowFramework.Extensions.AI;

/// <summary>
/// Configuration options for the Semantic Kernel agent provider.
/// </summary>
public sealed class SemanticKernelOptions
{
    /// <summary>Gets or sets the service ID to resolve from the kernel.</summary>
    public string? ServiceId { get; set; }
}

/// <summary>
/// An <see cref="IAgentProvider"/> that delegates to a Semantic Kernel <see cref="Kernel"/>.
/// </summary>
public sealed class SemanticKernelAgentProvider : IAgentProvider
{
    private readonly Kernel _kernel;
    private readonly SemanticKernelOptions _options;

    /// <summary>Initializes a new instance wrapping the given kernel.</summary>
    public SemanticKernelAgentProvider(Kernel kernel, SemanticKernelOptions? options = null)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _options = options ?? new SemanticKernelOptions();
    }

    /// <inheritdoc />
    public string Name => "SemanticKernel";

    /// <inheritdoc />
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>(_options.ServiceId);

        var history = new ChatHistory();

        // Add variables as system context
        var variableContext = FormatVariables(request.Variables);
        if (!string.IsNullOrWhiteSpace(variableContext))
        {
            history.AddSystemMessage("Context variables:\n" + variableContext);
        }

        history.AddUserMessage(request.Prompt);

        var settings = new PromptExecutionSettings();
        if (request.Temperature.HasValue || request.MaxTokens.HasValue)
        {
            var ext = new Dictionary<string, object>();
            if (request.Temperature.HasValue) ext["temperature"] = request.Temperature.Value;
            if (request.MaxTokens.HasValue) ext["max_tokens"] = request.MaxTokens.Value;
            settings.ExtensionData = ext;
        }

        // Auto-invoke tool calls if kernel has plugins registered
        if (_kernel.Plugins.Count > 0)
        {
            settings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto();
        }

        var result = await chatService.GetChatMessageContentsAsync(
            history, settings, _kernel, cancellationToken).ConfigureAwait(false);

        var response = new LlmResponse();

        if (result.Count > 0)
        {
            var last = result[^1];
            response.Content = last.Content ?? string.Empty;

            // Map metadata
            if (last.Metadata is not null)
            {
                if (last.Metadata.TryGetValue("FinishReason", out var fr))
                    response.FinishReason = fr?.ToString();
                if (last.Metadata.TryGetValue("Usage", out var usage) && usage is not null)
                {
                    // Try to extract token info from metadata
                    try
                    {
                        var json = JsonSerializer.Serialize(usage);
                        var usageDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                        if (usageDict is not null)
                        {
                            response.Usage = new TokenUsage
                            {
                                PromptTokens = usageDict.TryGetValue("PromptTokens", out var pt) ? pt.GetInt32() :
                                    usageDict.TryGetValue("InputTokenCount", out var it) ? it.GetInt32() : 0,
                                CompletionTokens = usageDict.TryGetValue("CompletionTokens", out var ct2) ? ct2.GetInt32() :
                                    usageDict.TryGetValue("OutputTokenCount", out var ot) ? ot.GetInt32() : 0,
                            };
                            response.Usage.TotalTokens = response.Usage.PromptTokens + response.Usage.CompletionTokens;
                        }
                    }
                    catch { /* best effort */ }
                }
            }

            // Map function call results from intermediate messages
            foreach (var msg in result)
            {
                if (msg.Items is not null)
                {
                    foreach (var item in msg.Items)
                    {
                        if (item is FunctionCallContent fcc)
                        {
                            response.ToolCalls.Add(new ToolCall
                            {
                                ToolName = fcc.FunctionName,
                                Arguments = fcc.Arguments is not null
                                    ? JsonSerializer.Serialize(fcc.Arguments)
                                    : "{}"
                            });
                        }
                    }
                }
            }
        }

        return response;
    }

    /// <inheritdoc />
    public async Task<string> DecideAsync(AgentDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>(_options.ServiceId);

        var optionsList = string.Join(", ", request.Options.Select(o => $"\"{o}\""));
        var variableContext = FormatVariables(request.Variables);

        var history = new ChatHistory();
        history.AddSystemMessage(
            $"You are a routing decision agent. You MUST respond with exactly one of these options: {optionsList}\n" +
            "Do not include any other text, explanation, or formatting. Just the option word.");

        var userPrompt = string.IsNullOrWhiteSpace(variableContext)
            ? request.Prompt
            : $"{request.Prompt}\n\nContext:\n{variableContext}";
        history.AddUserMessage(userPrompt);

        var result = await chatService.GetChatMessageContentsAsync(
            history, null, _kernel, cancellationToken).ConfigureAwait(false);

        var rawDecision = (result.LastOrDefault()?.Content ?? string.Empty).Trim();

        // Match against options (case-insensitive)
        foreach (var option in request.Options)
        {
            if (rawDecision.Contains(option, StringComparison.OrdinalIgnoreCase))
                return option;
        }

        return rawDecision.Length <= 50 ? rawDecision : request.Options.FirstOrDefault() ?? rawDecision;
    }

    private static string FormatVariables(IDictionary<string, object?> variables)
    {
        if (variables.Count == 0) return string.Empty;
        return string.Join("\n", variables
            .Where(kv => kv.Value is not null)
            .Select(kv => $"- {kv.Key}: {kv.Value}"));
    }
}
#endif
