using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkflowFramework.Extensions.AI;

/// <summary>
/// Configuration options for the Anthropic agent provider.
/// </summary>
public sealed class AnthropicOptions
{
    /// <summary>Gets or sets the API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the default model.</summary>
    public string DefaultModel { get; set; } = "claude-sonnet-4-20250514";

    /// <summary>Gets or sets the base URL.</summary>
    public string BaseUrl { get; set; } = "https://api.anthropic.com";

    /// <summary>Gets or sets the temperature.</summary>
    public double? Temperature { get; set; }

    /// <summary>Gets or sets the max tokens.</summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>Gets or sets the request timeout.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>Gets or sets the Anthropic API version header.</summary>
    public string AnthropicVersion { get; set; } = "2023-06-01";
}

/// <summary>
/// An <see cref="IAgentProvider"/> implementation that calls the Anthropic Messages API.
/// </summary>
public sealed class AnthropicAgentProvider : IAgentProvider, IDisposable
{
    private readonly HttpClient _http;
    private readonly AnthropicOptions _options;
    private readonly bool _ownsClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Initializes a new instance with the specified options.</summary>
    public AnthropicAgentProvider(AnthropicOptions options) : this(options, null) { }

    /// <summary>Initializes a new instance with the specified options and HttpClient.</summary>
    public AnthropicAgentProvider(AnthropicOptions options, HttpClient? httpClient)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ownsClient = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = options.Timeout };
    }

    /// <inheritdoc />
    public string Name => "Anthropic";

    /// <inheritdoc />
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var variableContext = FormatVariables(request.Variables);
        string? systemPrompt = !string.IsNullOrWhiteSpace(variableContext)
            ? "Context variables:\n" + variableContext
            : null;

        var messages = new List<AnthropicMessage>
        {
            new() { Role = "user", Content = request.Prompt }
        };

        var body = new AnthropicChatRequest
        {
            Model = request.Model ?? _options.DefaultModel,
            Messages = messages,
            System = systemPrompt,
            MaxTokens = request.MaxTokens ?? _options.MaxTokens,
            Temperature = request.Temperature ?? _options.Temperature
        };

        if (request.Tools.Count > 0)
        {
            body.Tools = request.Tools.Select(t => new AnthropicTool
            {
                Name = t.Name,
                Description = t.Description,
                InputSchema = string.IsNullOrEmpty(t.ParametersSchema)
                    ? null
                    : JsonSerializer.Deserialize<JsonElement>(t.ParametersSchema!)
            }).ToList();
        }

        var apiResponse = await SendAsync(body, cancellationToken).ConfigureAwait(false);

        var response = new LlmResponse
        {
            FinishReason = apiResponse.StopReason
        };

        if (apiResponse.Usage != null)
        {
            response.Usage = new TokenUsage
            {
                PromptTokens = apiResponse.Usage.InputTokens,
                CompletionTokens = apiResponse.Usage.OutputTokens,
                TotalTokens = apiResponse.Usage.InputTokens + apiResponse.Usage.OutputTokens
            };
        }

        if (apiResponse.Content != null)
        {
            var textParts = new List<string>();
            foreach (var block in apiResponse.Content)
            {
                if (block.Type == "text")
                {
                    textParts.Add(block.Text ?? string.Empty);
                }
                else if (block.Type == "tool_use")
                {
                    response.ToolCalls.Add(new ToolCall
                    {
                        ToolName = block.Name ?? string.Empty,
                        Arguments = block.Input.HasValue ? block.Input.Value.GetRawText() : "{}"
                    });
                }
            }
            response.Content = string.Join("", textParts);
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

        var messages = new List<AnthropicMessage>
        {
            new() { Role = "user", Content = userPrompt }
        };

        var body = new AnthropicChatRequest
        {
            Model = _options.DefaultModel,
            Messages = messages,
            System = systemPrompt,
            MaxTokens = _options.MaxTokens
        };

        var apiResponse = await SendAsync(body, cancellationToken).ConfigureAwait(false);
        var rawDecision = string.Empty;
        if (apiResponse.Content != null)
        {
            var textBlock = apiResponse.Content.FirstOrDefault(b => b.Type == "text");
            rawDecision = (textBlock?.Text ?? string.Empty).Trim();
        }

        foreach (var option in request.Options)
        {
            if (rawDecision.IndexOf(option, StringComparison.OrdinalIgnoreCase) >= 0)
                return option;
        }

        return rawDecision.Length <= 50 ? rawDecision : request.Options.FirstOrDefault() ?? rawDecision;
    }

    private async Task<AnthropicChatResponse> SendAsync(AnthropicChatRequest body, CancellationToken ct)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/v1/messages";
        var json = JsonSerializer.Serialize(body, JsonOptions);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        httpRequest.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);
        httpRequest.Headers.TryAddWithoutValidation("anthropic-version", _options.AnthropicVersion);

        using var httpResponse = await _http.SendAsync(httpRequest, ct).ConfigureAwait(false);
        httpResponse.EnsureSuccessStatusCode();

        var responseJson = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<AnthropicChatResponse>(responseJson, JsonOptions)
               ?? throw new InvalidOperationException("Anthropic returned null response");
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

    // --- Anthropic API DTOs ---

    private sealed class AnthropicChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<AnthropicMessage> Messages { get; set; } = new();
        public string? System { get; set; }
        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }
        public double? Temperature { get; set; }
        public List<AnthropicTool>? Tools { get; set; }
    }

    private sealed class AnthropicMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private sealed class AnthropicTool
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        [JsonPropertyName("input_schema")]
        public JsonElement? InputSchema { get; set; }
    }

    private sealed class AnthropicChatResponse
    {
        public List<AnthropicContentBlock>? Content { get; set; }
        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }
        public AnthropicUsage? Usage { get; set; }
    }

    private sealed class AnthropicContentBlock
    {
        public string Type { get; set; } = string.Empty;
        public string? Text { get; set; }
        public string? Name { get; set; }
        public JsonElement? Input { get; set; }
    }

    private sealed class AnthropicUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }
        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }
}
