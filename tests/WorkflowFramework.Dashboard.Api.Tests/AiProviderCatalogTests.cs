using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Services;
using Xunit;

namespace WorkflowFramework.Dashboard.Api.Tests;

public class AiProviderCatalogTests
{
    [Fact]
    public void OrderModels_PrefersChatFriendlyOllamaModels()
    {
        var ordered = AiProviderCatalog.OrderModels("ollama",
        [
            "qwen3.5:9b",
            "nomic-embed-text:latest",
            "llama3.2-vision:latest",
            "qwen3:30b-instruct"
        ]);

        ordered.Should().Equal(
            "qwen3:30b-instruct",
            "qwen3.5:9b",
            "llama3.2-vision:latest",
            "nomic-embed-text:latest");
    }

    [Fact]
    public void OrderModels_FiltersEmptyValues()
    {
        var ordered = AiProviderCatalog.OrderModels("ollama",
        [
            "",
            "   ",
            "qwen3:30b-instruct"
        ]);

        ordered.Should().Equal("qwen3:30b-instruct");
    }
}
