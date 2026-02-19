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
                DefaultModel = "qwen3:30b-instruct",
                Timeout = TimeSpan.FromSeconds(300),
                DisableThinking = true
            });

            // Warm up: force model load so first test doesn't eat the load time
            try
            {
                await Provider.CompleteAsync(new LlmRequest { Prompt = "Hi /no_think" });
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
