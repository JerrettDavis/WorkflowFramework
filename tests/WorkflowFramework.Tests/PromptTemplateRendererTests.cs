using FluentAssertions;
using WorkflowFramework.Extensions.AI;
using Xunit;

namespace WorkflowFramework.Tests;

public sealed class PromptTemplateRendererTests
{
    [Fact]
    public void Render_ReplacesLegacyInputTokens()
    {
        var rendered = PromptTemplateRenderer.Render(
            "Summarize {transcript}",
            new Dictionary<string, object?> { ["transcript"] = "hello world" });

        rendered.Should().Be("Summarize hello world");
    }

    [Fact]
    public void Render_ReplacesStepOutputTokens_WithSpacesInStepNames()
    {
        var rendered = PromptTemplateRenderer.Render(
            "Use {{Fetch Customer.Body}} to draft a response",
            new Dictionary<string, object?>
            {
                ["Fetch Customer.Body"] = "{\"name\":\"Ada\"}"
            });

        rendered.Should().Be("Use {\"name\":\"Ada\"} to draft a response");
    }

    [Fact]
    public void Render_ReplacesNestedDictionaryTokens()
    {
        var rendered = PromptTemplateRenderer.Render(
            "Customer: {{customer.profile.name}}",
            new Dictionary<string, object?>
            {
                ["customer"] = new Dictionary<string, object?>
                {
                    ["profile"] = new Dictionary<string, object?>
                    {
                        ["name"] = "Ada"
                    }
                }
            });

        rendered.Should().Be("Customer: Ada");
    }
}
