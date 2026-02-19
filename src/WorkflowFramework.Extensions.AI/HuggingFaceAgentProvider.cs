using System.Text.Json;
using System.Text.Json.Serialization;

namespace WorkflowFramework.Extensions.AI;

/// <summary>
/// Configuration options for the HuggingFace agent provider.
/// </summary>
public sealed class HuggingFaceOptions
{
    /// <summary>Gets or sets the API key.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the default model.</summary>
    public string DefaultModel { get; set; } = "mistralai/Mistral-7B-Instruct-v0.3";

    /// <summary>Gets or sets the base URL.</summary>
    public string BaseUrl { get; set; } = "https://api-inference.huggingface.co";

    /// <summary>Gets or sets the temperature.</summary>
    public double? Temperature { get; set; }

    /// <summary>Gets or sets the max tokens.</summary>
    public int? MaxTokens { get; set; }

    /// <summary>Gets or sets the request timeout.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(120);
}

/// <summary>
/// An <see cref="IAgentProvider"/> implementation that calls the HuggingFace Inference API.
/// </summary>
public sealed class HuggingFaceAgentProvider : IAgentProvider, IDisposable
{
    private readonly HttpClient _http;
    private readonly HuggingFaceOptions _options;
    private readonly bool _ownsClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Initializes a new instance with the specified options.</summary>
    public HuggingFaceAgentProvider(HuggingFaceOptions options) : this(options, null) { }

    /// <summary>Initializes a new instance with the specified options and HttpClient.</summary>
    public HuggingFaceAgentProvider(HuggingFaceOptions options, HttpClient? httpClient)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _ownsClient = httpClient is null;
        _http = httpClient ?? new HttpClient { Timeout = options.Timeout };
    }

    /// <inheritdoc />
    public string Name => "HuggingFace";

    /// <inheritdoc />
    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var prompt = request.Prompt;
        var variableContext = FormatVariables(request.Variables);
        if (!string.IsNullOrWhiteSpace(variableContext))
        {
            prompt = $"Context variables:\n{variableContext}\n\n{prompt}";
        }

        // Include tool descriptions in prompt if tools provided
        if (request.Tools.Count > 0)
        {
            var toolDesc = string.Join("\n", request.Tools.Select(t =>
                $"- {t.Name}: {t.Description}" + (string.IsNullOrEmpty(t.ParametersSchema) ? "" : $" Parameters: {t.ParametersSchema}")));
            prompt = $"Available tools:\n{toolDesc}\n\nIf you need to call a tool, respond with JSON: {{\"tool_name\": \"<name>\", \"arguments\": {{...}}}}\n\n{prompt}";
        }

        var model = request.Model ?? _options.DefaultModel;

        var body = new HuggingFaceRequest
        {
            Inputs = prompt,
            Parameters = new HuggingFaceParameters
            {
                Temperature = request.Temperature ?? _options.Temperature,
                MaxNewTokens = request.MaxTokens ?? _options.MaxTokens,
                ReturnFullText = false
            }
        };

        var apiResponse = await SendAsync(model, body, cancellationToken).ConfigureAwait(false);

        var generatedText = string.Empty;
        if (apiResponse != null && apiResponse.Count > 0)
        {
            generatedText = apiResponse[0].GeneratedText ?? string.Empty;
        }

        var response = new LlmResponse
        {
            Content = generatedText,
            FinishReason = "stop"
        };

        // Try to parse tool calls from response
        TryParseToolCalls(generatedText, response);

        return response;
    }

    /// <inheritdoc />
    public async Task<string> DecideAsync(AgentDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var optionsList = string.Join(", ", request.Options.Select(o => $"\"{o}\""));
        var variableContext = FormatVariables(request.Variables);

        var prompt = $"You are a routing decision agent. You MUST respond with exactly one of these options: {optionsList}\nDo not include any other text, explanation, or formatting. Just the option word.\n\n";

        if (!string.IsNullOrWhiteSpace(variableContext))
        {
            prompt += $"Context:\n{variableContext}\n\n";
        }

        prompt += request.Prompt;

        var model = _options.DefaultModel;
        var body = new HuggingFaceRequest
        {
            Inputs = prompt,
            Parameters = new HuggingFaceParameters
            {
                Temperature = _options.Temperature,
                MaxNewTokens = _options.MaxTokens ?? 50,
                ReturnFullText = false
            }
        };

        var apiResponse = await SendAsync(model, body, cancellationToken).ConfigureAwait(false);
        var rawDecision = string.Empty;
        if (apiResponse != null && apiResponse.Count > 0)
        {
            rawDecision = (apiResponse[0].GeneratedText ?? string.Empty).Trim();
        }

        foreach (var option in request.Options)
        {
            if (rawDecision.IndexOf(option, StringComparison.OrdinalIgnoreCase) >= 0)
                return option;
        }

        return rawDecision.Length <= 50 ? rawDecision : request.Options.FirstOrDefault() ?? rawDecision;
    }

    private async Task<List<HuggingFaceResponseItem>> SendAsync(string model, HuggingFaceRequest body, CancellationToken ct)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/models/{model}";
        var json = JsonSerializer.Serialize(body, JsonOptions);
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        httpRequest.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_options.ApiKey}");

        using var httpResponse = await _http.SendAsync(httpRequest, ct).ConfigureAwait(false);
        httpResponse.EnsureSuccessStatusCode();

        var responseJson = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<HuggingFaceResponseItem>>(responseJson, JsonOptions)
               ?? throw new InvalidOperationException("HuggingFace returned null response");
    }

    private static void TryParseToolCalls(string text, LlmResponse response)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("{")) return;

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;
            if (root.TryGetProperty("tool_name", out var nameEl))
            {
                var toolName = nameEl.GetString() ?? string.Empty;
                var args = root.TryGetProperty("arguments", out var argsEl)
                    ? argsEl.GetRawText()
                    : "{}";
                response.ToolCalls.Add(new ToolCall { ToolName = toolName, Arguments = args });
            }
        }
        catch (JsonException)
        {
            // Not valid JSON tool call, ignore
        }
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

    // --- HuggingFace API DTOs ---

    private sealed class HuggingFaceRequest
    {
        public string Inputs { get; set; } = string.Empty;
        public HuggingFaceParameters? Parameters { get; set; }
    }

    private sealed class HuggingFaceParameters
    {
        public double? Temperature { get; set; }
        [JsonPropertyName("max_new_tokens")]
        public int? MaxNewTokens { get; set; }
        [JsonPropertyName("return_full_text")]
        public bool? ReturnFullText { get; set; }
    }

    private sealed class HuggingFaceResponseItem
    {
        [JsonPropertyName("generated_text")]
        public string? GeneratedText { get; set; }
    }
}
