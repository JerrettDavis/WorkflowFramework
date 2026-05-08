using System.Text.Json;

namespace WorkflowFramework.Dashboard.Api.Services;

internal static class AiProviderCatalog
{
    private static readonly IReadOnlyList<string> ProvidersInternal =
    [
        "ollama",
        "openai",
        "anthropic",
        "huggingface"
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultModelsInternal =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ollama"] =
            [
                "llama3.1",
                "llama3.2",
                "mistral",
                "codellama",
                "phi3",
                "gemma2",
                "qwen2.5"
            ],
            ["openai"] =
            [
                "gpt-4o",
                "gpt-4o-mini",
                "gpt-4-turbo",
                "gpt-4",
                "gpt-3.5-turbo",
                "o1-preview",
                "o1-mini"
            ],
            ["anthropic"] =
            [
                "claude-opus-4-20250514",
                "claude-sonnet-4-20250514",
                "claude-3-5-haiku-20241022",
                "claude-3-opus-20240229"
            ],
            ["huggingface"] =
            [
                "meta-llama/Llama-3-70b-chat-hf",
                "mistralai/Mixtral-8x7B-Instruct-v0.1",
                "microsoft/Phi-3-mini-4k-instruct"
            ]
        };

    public static IReadOnlyList<string> Providers => ProvidersInternal;

    public static IReadOnlyList<string> GetDefaultModels(string provider)
        => DefaultModelsInternal.TryGetValue(provider, out var models) ? OrderModels(provider, models) : [];

    public static IReadOnlyList<string> OrderModels(string provider, IEnumerable<string> models)
        => models
            .Where(static model => !string.IsNullOrWhiteSpace(model))
            .OrderByDescending(model => GetModelScore(provider, model))
            .ThenBy(model => model, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static string SerializeProviderOptions()
        => JsonSerializer.Serialize(ProvidersInternal);

    public static string SerializeModelOptionGroups()
        => JsonSerializer.Serialize(DefaultModelsInternal.ToDictionary(
            entry => entry.Key,
            entry => OrderModels(entry.Key, entry.Value),
            StringComparer.OrdinalIgnoreCase));

    private static int GetModelScore(string provider, string model)
    {
        if (!provider.Equals("ollama", StringComparison.OrdinalIgnoreCase))
            return 0;

        var normalized = model.ToLowerInvariant();
        var score = 0;

        if (normalized.Contains("instruct", StringComparison.Ordinal))
            score += 50;
        if (normalized.Contains("chat", StringComparison.Ordinal))
            score += 40;
        if (normalized.Contains("assistant", StringComparison.Ordinal))
            score += 20;

        if (normalized.Contains("vision", StringComparison.Ordinal))
            score -= 20;

        if (normalized.Contains("embed", StringComparison.Ordinal)
            || normalized.Contains("embedding", StringComparison.Ordinal)
            || normalized.Contains("rerank", StringComparison.Ordinal)
            || normalized.Contains("whisper", StringComparison.Ordinal))
        {
            score -= 100;
        }

        return score;
    }
}
