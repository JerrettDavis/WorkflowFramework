using Xunit;
using WorkflowFramework.Extensions.AI;

namespace WorkflowFramework.Tests.E2E;

/// <summary>
/// Shared fixture that checks Ollama availability and provides a reusable provider.
/// </summary>
public sealed class OllamaFixture : IAsyncLifetime, IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    public OllamaAgentProvider Provider { get; private set; } = null!;
    public bool IsAvailable { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            using var response = await _http.GetAsync("http://localhost:11434/api/tags");
            IsAvailable = response.IsSuccessStatusCode;
        }
        catch
        {
            IsAvailable = false;
        }

        if (IsAvailable)
        {
            Provider = new OllamaAgentProvider(new OllamaOptions
            {
                DefaultModel = "qwen2.5:1.5b",
                Timeout = TimeSpan.FromSeconds(120),
                DisableThinking = true
            });

            // Warm up: force model load so first test doesn't eat the load time
            try
            {
                await Provider.CompleteAsync(new LlmRequest { Prompt = "Hi" });
            }
            catch { /* best effort warmup */ }
        }
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Provider?.Dispose();
        _http.Dispose();
    }
}

[CollectionDefinition("Ollama")]
public class OllamaCollection : ICollectionFixture<OllamaFixture>;

/// <summary>
/// A fact attribute that automatically skips the test when Ollama is not reachable
/// at <c>http://localhost:11434</c>.  No Ollama dependency means no flaky CI failures.
/// </summary>
public sealed class OllamaFactAttribute : FactAttribute
{
    private const string OllamaUrl = "http://localhost:11434/api/tags";
    private const int ProbeTimeoutSeconds = 3;

    public OllamaFactAttribute()
    {
        if (!IsOllamaReachable())
            Skip = "SKIP: Ollama is not reachable at http://localhost:11434 — install Ollama and run 'ollama pull qwen2.5:1.5b' to run this test.";
    }

    private static bool IsOllamaReachable()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(ProbeTimeoutSeconds) };
            using var response = http.GetAsync(OllamaUrl).GetAwaiter().GetResult();
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
