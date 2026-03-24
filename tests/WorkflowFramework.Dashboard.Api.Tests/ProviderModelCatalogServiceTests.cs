using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WorkflowFramework.Dashboard.Api.Services;
using Xunit;

namespace WorkflowFramework.Dashboard.Api.Tests;

public class ProviderModelCatalogServiceTests
{
    [Fact]
    public async Task GetModelsAsync_UsesExplicitOllamaUrlWhenProvided()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            var payload = JsonSerializer.Serialize(new
            {
                models = new[]
                {
                    new { name = "qwen3:30b-instruct" },
                    new { name = "llama3.2:latest" }
                }
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });

        var service = CreateService(handler);
        var settings = new DashboardSettings { OllamaUrl = "http://localhost:11434" };

        var models = await service.GetModelsAsync("ollama", "http://127.0.0.1:22444", settings);

        handler.RequestUris.Should().ContainSingle();
        handler.RequestUris[0].Should().Be(new Uri("http://127.0.0.1:22444/api/tags"));
        models.Should().Contain(new[] { "qwen3:30b-instruct", "llama3.2:latest" });
    }

    [Fact]
    public async Task GetModelsAsync_ReturnsEmptyListWhenOllamaDiscoveryFails()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("boom"));
        var service = CreateService(handler);
        var settings = new DashboardSettings { OllamaUrl = "http://localhost:11434" };

        var models = await service.GetModelsAsync("ollama", null, settings);

        models.Should().BeEmpty();
    }

    [Fact]
    public async Task GetModelsAsync_PreservesBasePathWhenBuildingTagsEndpoint()
    {
        var handler = new StubHttpMessageHandler(_ =>
        {
            var payload = JsonSerializer.Serialize(new
            {
                models = new[]
                {
                    new { name = "qwen3:30b-instruct" }
                }
            });

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });

        var service = CreateService(handler);
        var settings = new DashboardSettings { OllamaUrl = "http://localhost:11434" };

        _ = await service.GetModelsAsync("ollama", "http://127.0.0.1:22444/ollama/", settings);

        handler.RequestUris.Should().ContainSingle();
        handler.RequestUris[0].Should().Be(new Uri("http://127.0.0.1:22444/ollama/api/tags"));
    }

    [Fact]
    public void ReadOllamaModelNames_ReadsNameAndModelFields_WithoutDuplicates()
    {
        using var document = JsonDocument.Parse("""
            {
              "models": [
                { "name": "qwen3:30b-instruct" },
                { "model": "llama3.2:latest" },
                { "name": "qwen3:30b-instruct" },
                { "name": "   " },
                { "model": "" }
              ]
            }
            """);

        var models = ProviderModelCatalogService.ReadOllamaModelNames(document.RootElement);

        models.Should().Equal("qwen3:30b-instruct", "llama3.2:latest");
    }

    private static ProviderModelCatalogService CreateService(HttpMessageHandler handler)
        => new(new StubHttpClientFactory(handler), NullLogger<ProviderModelCatalogService>.Instance);

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            return Task.FromResult(responder(request));
        }
    }
}
