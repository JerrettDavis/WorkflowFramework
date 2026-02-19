using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkflowFramework.Extensions.AI;

/// <summary>
/// Configuration options for the OpenAI agent provider.
/// </summary>
public sealed class OpenAiOptions
{
    /// <summary>Gets or sets the API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the default model.</summary>
    public string DefaultModel { get; set; } = "gpt-4o";

    /// <summary>Gets or sets the base URL.</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>Gets or sets the temperature.</summary>
    public double? Temperature { get; set; }

    /// <summary>Gets or sets the max tokens.</summary>
    public int? MaxTokens { get; set; }

    /// <summary>Gets or sets the request timeout.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(120);
}

/// <summary>
/// An <see cref="IAgentProvider"/> implementation that calls the OpenAI Chat Completions API.
/// </summary>
public sealed class OpenAiAgentProvider : IAgentProvider, IDisposable
{
    private readonly HttpClient _http;
    private readonly OpenAiOptions _options;
    private readonly bool _ownsClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Initializes a new instance with the specified options.</summary>
    public OpenAiAgentProvider(OpenAiOptions options) : this(options, null) { }

    /// <summary>Initializes a new instance with the specified options and HttpClient.</summary>
    public OpenAiAgentProvider(OpenAiOptions options, HttpClient? httpClient)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ownsClient = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = options.Timeout };
    }

    /// <inheritdoc />
    public string Name => "OpenAI";

    /// <inheritdoc />
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var messages = new List<OpenAiMessage>();

        var variableContext = FormatVariables(request.Variables);
        if (!string.IsNullOrWhiteSpace(variableContext))
        {
            messages.Add(new OpenAiMessage { Role = "system", Content = "Context variables:\n" + variableContext });
        }

        messages.Add(new OpenAiMessage { Role = "user", Content = request.Prompt });

        var body = new OpenAiChatRequest
        {
            Model = request.Model ?? _options.DefaultModel,
            Messages = messages,
            Temperature = request.Temperature ?? _options.Temperature,
            MaxTokens = request.MaxTokens ?? _options.MaxTokens
        };

        if (request.Tools.Count > 0)
        {
            body.Tools = request.Tools.Select(t => new OpenAiTool
            {
                Type = "function",
                Function = new OpenAiToolFunction
                {
                    Name = t.Name,
                    Description = t.Description,
                    Parameters = string.IsNullOrEmpty(t.ParametersSchema)
                        ? null
                        : JsonSerializer.Deserialize<JsonElement>(t.ParametersSchema!)
                }
            }).ToList();
        }

        var apiResponse = await SendAsync(body, cancellationToken).ConfigureAwait(false);
        var choice = apiResponse.Choices?.Count > 0 ? apiResponse.Choices[0] : null;
        var msg = choice?.Message;

        var response = new LlmResponse
        {
            Content = msg?.Content ?? string.Empty,
            FinishReason = choice?.FinishReason
        };

        if (apiResponse.Usage != null)
        {
            response.Usage = new TokenUsage
            {
                PromptTokens = apiResponse.Usage.PromptTokens,
                CompletionTokens = apiResponse.Usage.CompletionTokens,
                TotalTokens = apiResponse.Usage.PromptTokens + apiResponse.Usage.CompletionTokens
            };
        }

        if (msg?.ToolCalls is { Count: > 0 } toolCalls)
        {
            foreach (var tc in toolCalls)
            {
                response.ToolCalls.Add(new ToolCall
                {
                    ToolName = tc.Function?.Name ?? string.Empty,
                    Arguments = tc.Function?.Arguments ?? "{}"
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

        var systemPrompt = $"You are a routing decision agent. You MUST respond with exactly one of these options: {optionsList}\nDo not include any other text, explanation, or formatting. Just the option word.";

        var userPrompt = string.IsNullOrWhiteSpace(variableContext)
            ? request.Prompt
            : $"{request.Prompt}\n\nContext:\n{variableContext}";

        var messages = new List<OpenAiMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = userPrompt }
        };

        var body = new OpenAiChatRequest
        {
            Model = _options.DefaultModel,
            Messages = messages
        };

        var apiResponse = await SendAsync(body, cancellationToken).ConfigureAwait(false);
        var rawDecision = (apiResponse.Choices?.Count > 0 ? apiResponse.Choices[0].Message?.Content : null)?.Trim() ?? string.Empty;

        foreach (var option in request.Options)
        {
            if (rawDecision.IndexOf(option, StringComparison.OrdinalIgnoreCase) >= 0)
                return option;
        }

        return rawDecision.Length <= 50 ? rawDecision : request.Options.FirstOrDefault() ?? rawDecision;
    }

    private async Task<OpenAiChatResponse> SendAsync(OpenAiChatRequest body, CancellationToken ct)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/chat/completions";
        var json = JsonSerializer.Serialize(body, JsonOptions);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_options.ApiKey}");

        using var httpResponse = await _http.SendAsync(httpRequest, ct).ConfigureAwait(false);
        httpResponse.EnsureSuccessStatusCode();

        var responseJson = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<OpenAiChatResponse>(responseJson, JsonOptions)
               ?? throw new InvalidOperationException("OpenAI returned null response");
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

    // --- OpenAI API DTOs ---

    private sealed class OpenAiChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<OpenAiMessage> Messages { get; set; } = new();
        public double? Temperature { get; set; }
        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; set; }
        public List<OpenAiTool>? Tools { get; set; }
    }

    private sealed class OpenAiMessage
    {
        public string Role { get; set; } = string.Empty;
        public string? Content { get; set; }
        [JsonPropertyName("tool_calls")]
        public List<OpenAiToolCall>? ToolCalls { get; set; }
    }

    private sealed class OpenAiTool
    {
        public string Type { get; set; } = "function";
        public OpenAiToolFunction Function { get; set; } = new();
    }

    private sealed class OpenAiToolFunction
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public JsonElement? Parameters { get; set; }
    }

    private sealed class OpenAiToolCall
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = "function";
        public OpenAiToolCallFunction? Function { get; set; }
    }

    private sealed class OpenAiToolCallFunction
    {
        public string Name { get; set; } = string.Empty;
        public string Arguments { get; set; } = "{}";
    }

    private sealed class OpenAiChatResponse
    {
        public List<OpenAiChoice>? Choices { get; set; }
        public OpenAiUsage? Usage { get; set; }
    }

    private sealed class OpenAiChoice
    {
        public OpenAiMessage? Message { get; set; }
        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private sealed class OpenAiUsage
    {
        [JsonPropertyName("prompt_tokens")]
        public int PromptTokens { get; set; }
        [JsonPropertyName("completion_tokens")]
        public int CompletionTokens { get; set; }
    }
}
