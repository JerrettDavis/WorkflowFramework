using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkflowFramework.Extensions.AI;

/// <summary>
/// Configuration options for the Ollama agent provider.
/// </summary>
public sealed class OllamaOptions
{
    /// <summary>Gets or sets the Ollama base URL.</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Gets or sets the default model.</summary>
    public string DefaultModel { get; set; } = "qwen3:30b-instruct";

    /// <summary>Gets or sets the request timeout.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>Gets or sets whether to disable thinking/reasoning mode (appends /no_think for supported models).</summary>
    public bool DisableThinking { get; set; } = true;
}

/// <summary>
/// An <see cref="IAgentProvider"/> implementation that calls a local Ollama instance.
/// </summary>
public sealed class OllamaAgentProvider : IAgentProvider, IDisposable
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _options;
    private readonly bool _ownsClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Initializes a new instance with default options.</summary>
    public OllamaAgentProvider() : this(new OllamaOptions()) { }

    /// <summary>Initializes a new instance with the specified options.</summary>
    public OllamaAgentProvider(OllamaOptions options) : this(options, null) { }

    /// <summary>Initializes a new instance with the specified options and HttpClient.</summary>
    public OllamaAgentProvider(OllamaOptions options, HttpClient? httpClient)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ownsClient = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = options.Timeout };
    }

    /// <inheritdoc />
    public string Name => "Ollama";

    /// <inheritdoc />
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var messages = new List<OllamaChatMessage>();

        // Build system message from variables if present
        var variableContext = FormatVariables(request.Variables);
        if (!string.IsNullOrWhiteSpace(variableContext))
        {
            messages.Add(new OllamaChatMessage { Role = "system", Content = "Context variables:\n" + variableContext });
        }

        var userContent = _options.DisableThinking
            ? request.Prompt + " /no_think"
            : request.Prompt;
        messages.Add(new OllamaChatMessage { Role = "user", Content = userContent });

        var body = new OllamaChatRequest
        {
            Model = request.Model ?? _options.DefaultModel,
            Messages = messages,
            Stream = false,
            Options = BuildOptions(request)
        };

        // Add tools if provided
        if (request.Tools.Count > 0)
        {
            body.Tools = request.Tools.Select(t => new OllamaTool
            {
                Type = "function",
                Function = new OllamaToolFunction
                {
                    Name = t.Name,
                    Description = t.Description,
                    Parameters = string.IsNullOrEmpty(t.ParametersSchema)
                        ? null
                        : JsonSerializer.Deserialize<JsonElement>(t.ParametersSchema!)
                }
            }).ToList();
        }

        var ollamaResponse = await SendAsync(body, cancellationToken).ConfigureAwait(false);

        var response = new LlmResponse
        {
            Content = ollamaResponse.Message?.Content ?? string.Empty,
            FinishReason = ollamaResponse.DoneReason ?? (ollamaResponse.Done ? "stop" : null),
            Usage = new TokenUsage
            {
                PromptTokens = ollamaResponse.PromptEvalCount,
                CompletionTokens = ollamaResponse.EvalCount,
                TotalTokens = ollamaResponse.PromptEvalCount + ollamaResponse.EvalCount
            }
        };

        // Map tool calls if present
        if (ollamaResponse.Message?.ToolCalls is { Count: > 0 } toolCalls)
        {
            foreach (var tc in toolCalls)
            {
                response.ToolCalls.Add(new ToolCall
                {
                    ToolName = tc.Function?.Name ?? string.Empty,
                    Arguments = tc.Function?.Arguments.ToString() ?? "{}"
                });
            }
        }

        return response;
    }

    /// <inheritdoc />
    public async Task<string> DecideAsync(AgentDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var optionsList = string.Join(", ", request.Options.Select(o => $"\"{o}\""));
        var variableContext = FormatVariables(request.Variables);

        var systemPrompt = $"""
            You are a routing decision agent. You MUST respond with exactly one of these options: {optionsList}
            Do not include any other text, explanation, or formatting. Just the option word.
            """;

        var basePrompt = string.IsNullOrWhiteSpace(variableContext)
            ? request.Prompt
            : $"{request.Prompt}\n\nContext:\n{variableContext}";
        var userPrompt = _options.DisableThinking ? basePrompt + " /no_think" : basePrompt;

        var messages = new List<OllamaChatMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = userPrompt }
        };

        var body = new OllamaChatRequest
        {
            Model = _options.DefaultModel,
            Messages = messages,
            Stream = false
        };

        var ollamaResponse = await SendAsync(body, cancellationToken).ConfigureAwait(false);
        var rawDecision = (ollamaResponse.Message?.Content ?? string.Empty).Trim();

        // Try to match one of the options (case-insensitive)
        foreach (var option in request.Options)
        {
            if (rawDecision.Contains(option, StringComparison.OrdinalIgnoreCase))
                return option;
        }

        // Fallback: return raw if it's short enough, else first option
        return rawDecision.Length <= 50 ? rawDecision : request.Options.FirstOrDefault() ?? rawDecision;
    }

    private async Task<OllamaChatResponse> SendAsync(OllamaChatRequest body, CancellationToken ct)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/api/chat";
        var json = JsonSerializer.Serialize(body, JsonOptions);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var httpResponse = await _http.PostAsync(url, content, ct).ConfigureAwait(false);
        httpResponse.EnsureSuccessStatusCode();

        var responseJson = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<OllamaChatResponse>(responseJson, JsonOptions)
               ?? throw new InvalidOperationException("Ollama returned null response");
    }

    private static OllamaRequestOptions? BuildOptions(LlmRequest request)
    {
        if (request.Temperature is null && request.MaxTokens is null)
            return null;

        return new OllamaRequestOptions
        {
            Temperature = request.Temperature,
            NumPredict = request.MaxTokens
        };
    }

    private static string FormatVariables(IDictionary<string, object?> variables)
    {
        if (variables.Count == 0) return string.Empty;
        return string.Join("\n", variables
            .Where(kv => kv.Value is not null)
            .Select(kv => $"- {kv.Key}: {kv.Value}"));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsClient) _http.Dispose();
    }

    // --- Ollama API DTOs ---

    private sealed class OllamaChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<OllamaChatMessage> Messages { get; set; } = [];
        public bool Stream { get; set; }
        public OllamaRequestOptions? Options { get; set; }
        public List<OllamaTool>? Tools { get; set; }
    }

    private sealed class OllamaChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        [JsonPropertyName("tool_calls")]
        public List<OllamaToolCall>? ToolCalls { get; set; }
    }

    private sealed class OllamaRequestOptions
    {
        public double? Temperature { get; set; }
        [JsonPropertyName("num_predict")]
        public int? NumPredict { get; set; }
    }

    private sealed class OllamaTool
    {
        public string Type { get; set; } = "function";
        public OllamaToolFunction Function { get; set; } = new();
    }

    private sealed class OllamaToolFunction
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public JsonElement? Parameters { get; set; }
    }

    private sealed class OllamaToolCall
    {
        public OllamaToolCallFunction? Function { get; set; }
    }

    private sealed class OllamaToolCallFunction
    {
        public string Name { get; set; } = string.Empty;
        public JsonElement Arguments { get; set; }
    }

    private sealed class OllamaChatResponse
    {
        public OllamaChatMessage? Message { get; set; }
        public bool Done { get; set; }
        [JsonPropertyName("done_reason")]
        public string? DoneReason { get; set; }
        [JsonPropertyName("prompt_eval_count")]
        public int PromptEvalCount { get; set; }
        [JsonPropertyName("eval_count")]
        public int EvalCount { get; set; }
    }
}
