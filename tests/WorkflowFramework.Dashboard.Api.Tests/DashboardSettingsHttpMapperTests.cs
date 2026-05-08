using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Services;
using Xunit;

namespace WorkflowFramework.Dashboard.Api.Tests;

public class DashboardSettingsHttpMapperTests
{
    [Fact]
    public void ToResponse_HidesSecrets_AndExposesConfiguredFlags()
    {
        var settings = new DashboardSettings
        {
            OllamaUrl = "http://localhost:11434",
            OpenAiApiKey = "openai-secret",
            AnthropicApiKey = "anthropic-secret",
            HuggingFaceApiKey = "hf-secret",
            OpenAiBaseUrl = "https://api.openai.com/v1",
            DefaultProvider = "openai",
            DefaultModel = "gpt-4o-mini",
            DefaultTimeoutSeconds = 120,
            MaxConcurrentRuns = 3
        };

        var response = DashboardSettingsHttpMapper.ToResponse(settings);

        response.OllamaUrl.Should().Be("http://localhost:11434");
        response.OpenAiBaseUrl.Should().Be("https://api.openai.com/v1");
        response.DefaultProvider.Should().Be("openai");
        response.DefaultModel.Should().Be("gpt-4o-mini");
        response.DefaultTimeoutSeconds.Should().Be(120);
        response.MaxConcurrentRuns.Should().Be(3);
        response.OpenAiConfigured.Should().BeTrue();
        response.AnthropicConfigured.Should().BeTrue();
        response.HuggingFaceConfigured.Should().BeTrue();
    }

    [Fact]
    public void ApplyUpdate_PreservesExistingSecrets_WhenRequestLeavesThemBlank()
    {
        var current = new DashboardSettings
        {
            OpenAiApiKey = "openai-secret",
            AnthropicApiKey = "anthropic-secret",
            HuggingFaceApiKey = "hf-secret",
            DefaultProvider = "ollama",
            DefaultModel = "llama3.2"
        };

        var request = new UpdateDashboardSettingsRequest
        {
            OllamaUrl = "http://localhost:11434",
            OpenAiApiKey = "",
            AnthropicApiKey = null,
            HuggingFaceApiKey = "   ",
            DefaultProvider = "openai",
            DefaultModel = "gpt-4o-mini",
            DefaultTimeoutSeconds = 90,
            MaxConcurrentRuns = 2
        };

        var updated = DashboardSettingsHttpMapper.ApplyUpdate(current, request);

        updated.OpenAiApiKey.Should().Be("openai-secret");
        updated.AnthropicApiKey.Should().Be("anthropic-secret");
        updated.HuggingFaceApiKey.Should().Be("hf-secret");
        updated.DefaultProvider.Should().Be("openai");
        updated.DefaultModel.Should().Be("gpt-4o-mini");
        updated.DefaultTimeoutSeconds.Should().Be(90);
        updated.MaxConcurrentRuns.Should().Be(2);
    }

    [Fact]
    public void ApplyUpdate_ReplacesSecrets_WhenRequestProvidesNewValues()
    {
        var current = new DashboardSettings
        {
            OpenAiApiKey = "old-openai",
            AnthropicApiKey = "old-anthropic",
            HuggingFaceApiKey = "old-hf"
        };

        var request = new UpdateDashboardSettingsRequest
        {
            OllamaUrl = "http://localhost:11434",
            OpenAiApiKey = "new-openai",
            AnthropicApiKey = "new-anthropic",
            HuggingFaceApiKey = "new-hf"
        };

        var updated = DashboardSettingsHttpMapper.ApplyUpdate(current, request);

        updated.OpenAiApiKey.Should().Be("new-openai");
        updated.AnthropicApiKey.Should().Be("new-anthropic");
        updated.HuggingFaceApiKey.Should().Be("new-hf");
    }

    [Theory]
    [InlineData("ftp://localhost:11434", false)]
    [InlineData("http://localhost:11434", true)]
    [InlineData("https://127.0.0.1:11434", true)]
    [InlineData("http://example.com:11434", false)]
    public void TryCreateValidatedOllamaUri_AllowsOnlyAbsoluteLoopbackHttpUrls(string value, bool expectedValid)
    {
        var valid = DashboardSettingsHttpMapper.TryCreateValidatedOllamaUri(value, out var uri, out _);

        valid.Should().Be(expectedValid);
        if (expectedValid)
            uri.Should().NotBeNull();
    }

    [Theory]
    [InlineData("", 300, 5, true)]
    [InlineData("ollama", 300, 5, true)]
    [InlineData("openai", 1, 1, true)]
    [InlineData("invalid-provider", 300, 5, false)]
    [InlineData("ollama", 0, 5, false)]
    [InlineData("ollama", 300, 0, false)]
    public void TryValidateUpdate_RejectsInvalidProviderAndLimits(string provider, int timeoutSeconds, int maxConcurrentRuns, bool expectedValid)
    {
        var request = new UpdateDashboardSettingsRequest
        {
            OllamaUrl = "http://localhost:11434",
            DefaultProvider = provider,
            DefaultTimeoutSeconds = timeoutSeconds,
            MaxConcurrentRuns = maxConcurrentRuns
        };

        var valid = DashboardSettingsHttpMapper.TryValidateUpdate(request, out _);
        valid.Should().Be(expectedValid);
    }
}
