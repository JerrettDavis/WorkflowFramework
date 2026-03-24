using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace WorkflowFramework.Dashboard.Api.Services;

internal sealed class ProviderModelCatalogService(IHttpClientFactory factory, ILogger<ProviderModelCatalogService> logger)
{
    public async Task<IReadOnlyList<string>> GetModelsAsync(string provider, string? ollamaUrl, DashboardSettings settings, CancellationToken cancellationToken = default)
    {
        if (!provider.Equals("ollama", StringComparison.OrdinalIgnoreCase))
            return AiProviderCatalog.GetDefaultModels(provider);

        var candidateUrl = string.IsNullOrWhiteSpace(ollamaUrl)
            ? settings.OllamaUrl
            : ollamaUrl;

        if (!DashboardSettingsHttpMapper.TryCreateValidatedOllamaUri(candidateUrl, out var ollamaUri, out _))
            return [];

        try
        {
            var client = factory.CreateClient("OllamaClient");
            client.Timeout = TimeSpan.FromSeconds(10);
            using var response = await client.GetAsync(BuildTagsUri(ollamaUri!), cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            return AiProviderCatalog.OrderModels(provider, ReadOllamaModelNames(json));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Failed to interrogate Ollama models from {OllamaUrl}.", candidateUrl);
            return [];
        }
    }

    internal static Uri BuildTagsUri(Uri baseUri)
    {
        var normalized = baseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? baseUri
            : new Uri($"{baseUri.AbsoluteUri}/");

        return new Uri(normalized, "api/tags");
    }

    internal static IReadOnlyList<string> ReadOllamaModelNames(JsonElement json)
    {
        if (!json.TryGetProperty("models", out var modelsElement) || modelsElement.ValueKind != JsonValueKind.Array)
            return [];

        var models = new List<string>();
        foreach (var modelElement in modelsElement.EnumerateArray())
        {
            if (TryGetModelName(modelElement, out var modelName))
                models.Add(modelName);
        }

        return models
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool TryGetModelName(JsonElement modelElement, out string modelName)
    {
        modelName = string.Empty;

        if (TryReadStringProperty(modelElement, "name", out modelName))
            return true;

        return TryReadStringProperty(modelElement, "model", out modelName);
    }

    private static bool TryReadStringProperty(JsonElement modelElement, string propertyName, out string value)
    {
        value = string.Empty;
        if (!modelElement.TryGetProperty(propertyName, out var propertyElement) || propertyElement.ValueKind != JsonValueKind.String)
            return false;

        var rawValue = propertyElement.GetString();
        if (string.IsNullOrWhiteSpace(rawValue))
            return false;

        value = rawValue;
        return true;
    }
}
