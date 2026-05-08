namespace WorkflowFramework.Dashboard.Api.Services;

public sealed class DashboardSettingsResponse
{
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string? OpenAiBaseUrl { get; set; }
    public string? DefaultProvider { get; set; }
    public string? DefaultModel { get; set; }
    public int DefaultTimeoutSeconds { get; set; } = 300;
    public int MaxConcurrentRuns { get; set; } = 5;
    public bool OpenAiConfigured { get; set; }
    public bool AnthropicConfigured { get; set; }
    public bool HuggingFaceConfigured { get; set; }
}

public sealed class UpdateDashboardSettingsRequest
{
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string? OpenAiApiKey { get; set; }
    public string? AnthropicApiKey { get; set; }
    public string? HuggingFaceApiKey { get; set; }
    public string? OpenAiBaseUrl { get; set; }
    public string? DefaultProvider { get; set; }
    public string? DefaultModel { get; set; }
    public int DefaultTimeoutSeconds { get; set; } = 300;
    public int MaxConcurrentRuns { get; set; } = 5;
}

public sealed class TestOllamaConnectionRequest
{
    public string? OllamaUrl { get; set; }
}

public static class DashboardSettingsHttpMapper
{
    public static bool TryValidateUpdate(UpdateDashboardSettingsRequest request, out string error)
    {
        if (!TryValidateOllamaUrl(request.OllamaUrl, out error))
            return false;

        if (request.DefaultTimeoutSeconds is < 1 or > 3600)
        {
            error = "Default timeout must be between 1 and 3600 seconds.";
            return false;
        }

        if (request.MaxConcurrentRuns is < 1 or > 100)
        {
            error = "Max concurrent runs must be between 1 and 100.";
            return false;
        }

        var provider = request.DefaultProvider?.Trim();
        if (!string.IsNullOrWhiteSpace(provider) &&
            !AiProviderCatalog.Providers.Contains(provider, StringComparer.OrdinalIgnoreCase))
        {
            error = $"Unknown provider '{provider}'.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryCreateValidatedOllamaUri(string? value, out Uri? uri, out string error)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Ollama URL is required.";
            return false;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed))
        {
            error = "Ollama URL must be an absolute URL.";
            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "Ollama URL must use http or https.";
            return false;
        }

        if (!parsed.IsLoopback)
        {
            error = "Only loopback Ollama endpoints are allowed for dashboard settings.";
            return false;
        }

        uri = parsed;
        error = string.Empty;
        return true;
    }

    public static DashboardSettingsResponse ToResponse(DashboardSettings settings) => new()
    {
        OllamaUrl = settings.OllamaUrl,
        OpenAiBaseUrl = settings.OpenAiBaseUrl,
        DefaultProvider = settings.DefaultProvider,
        DefaultModel = settings.DefaultModel,
        DefaultTimeoutSeconds = settings.DefaultTimeoutSeconds,
        MaxConcurrentRuns = settings.MaxConcurrentRuns,
        OpenAiConfigured = !string.IsNullOrWhiteSpace(settings.OpenAiApiKey),
        AnthropicConfigured = !string.IsNullOrWhiteSpace(settings.AnthropicApiKey),
        HuggingFaceConfigured = !string.IsNullOrWhiteSpace(settings.HuggingFaceApiKey)
    };

    public static DashboardSettings ApplyUpdate(DashboardSettings current, UpdateDashboardSettingsRequest request) => new()
    {
        OllamaUrl = request.OllamaUrl,
        OpenAiApiKey = string.IsNullOrWhiteSpace(request.OpenAiApiKey) ? current.OpenAiApiKey : request.OpenAiApiKey,
        AnthropicApiKey = string.IsNullOrWhiteSpace(request.AnthropicApiKey) ? current.AnthropicApiKey : request.AnthropicApiKey,
        HuggingFaceApiKey = string.IsNullOrWhiteSpace(request.HuggingFaceApiKey) ? current.HuggingFaceApiKey : request.HuggingFaceApiKey,
        OpenAiBaseUrl = request.OpenAiBaseUrl,
        DefaultProvider = request.DefaultProvider,
        DefaultModel = request.DefaultModel,
        DefaultTimeoutSeconds = request.DefaultTimeoutSeconds,
        MaxConcurrentRuns = request.MaxConcurrentRuns
    };

    private static bool TryValidateOllamaUrl(string? value, out string error)
        => TryCreateValidatedOllamaUri(value, out _, out error);
}
