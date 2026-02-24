namespace WorkflowFramework.Dashboard.Api.Services;

public sealed class DashboardSettings
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

public sealed class DashboardSettingsService : IDashboardSettingsService
{
    private readonly Lock _lock = new();
    private DashboardSettings _settings = new();

    public DashboardSettings Get()
    {
        lock (_lock) return Clone(_settings);
    }

    public void Update(DashboardSettings settings)
    {
        lock (_lock) _settings = Clone(settings);
    }

    private static DashboardSettings Clone(DashboardSettings s) => new()
    {
        OllamaUrl = s.OllamaUrl,
        OpenAiApiKey = s.OpenAiApiKey,
        AnthropicApiKey = s.AnthropicApiKey,
        HuggingFaceApiKey = s.HuggingFaceApiKey,
        OpenAiBaseUrl = s.OpenAiBaseUrl,
        DefaultProvider = s.DefaultProvider,
        DefaultModel = s.DefaultModel,
        DefaultTimeoutSeconds = s.DefaultTimeoutSeconds,
        MaxConcurrentRuns = s.MaxConcurrentRuns
    };
}
