using Xunit;
using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Services;

namespace WorkflowFramework.Dashboard.Api.Tests;

public class WorkflowTemplateLibraryTests
{
    private readonly InMemoryWorkflowTemplateLibrary _library = new();

    [Fact]
    public async Task GetTemplatesAsync_ReturnsAllTemplates()
    {
        var templates = await _library.GetTemplatesAsync();
        templates.Should().HaveCount(25);
    }

    [Fact]
    public async Task GetCategoriesAsync_ReturnsExpectedCategories()
    {
        var categories = await _library.GetCategoriesAsync();
        categories.Should().Contain("Getting Started");
        categories.Should().Contain("Data Processing");
        categories.Should().Contain("Order Management");
        categories.Should().Contain("AI & Agents");
        categories.Should().Contain("Voice & Audio");
        categories.Should().Contain("Integration Patterns");
        categories.Should().HaveCount(6);
    }

    [Fact]
    public async Task GetTemplatesAsync_FiltersByCategory()
    {
        var templates = await _library.GetTemplatesAsync(category: "Getting Started");
        templates.Should().HaveCount(7);
        templates.Should().OnlyContain(t => t.Category == "Getting Started");
    }

    [Fact]
    public async Task GetTemplatesAsync_FiltersByTag()
    {
        var templates = await _library.GetTemplatesAsync(tag: "parallel");
        templates.Should().HaveCountGreaterThanOrEqualTo(3);
        templates.Should().OnlyContain(t => t.Tags.Contains("parallel"));
    }

    [Fact]
    public async Task GetTemplatesAsync_FiltersByCategoryAndTag()
    {
        var templates = await _library.GetTemplatesAsync(category: "Getting Started", tag: "conditional");
        templates.Should().HaveCount(1);
        templates.First().Id.Should().Be("conditional-branching");
    }

    [Fact]
    public async Task GetTemplateAsync_ReturnsNullForUnknown()
    {
        var template = await _library.GetTemplateAsync("nonexistent");
        template.Should().BeNull();
    }

    [Fact]
    public async Task GetTemplateAsync_IsCaseInsensitive()
    {
        var template = await _library.GetTemplateAsync("HELLO-WORLD");
        template.Should().NotBeNull();
    }

    [Theory]
    [InlineData("hello-world", "Hello World", "Getting Started", 1)]
    [InlineData("sequential-pipeline", "Sequential Pipeline", "Getting Started", 3)]
    [InlineData("conditional-branching", "Conditional Branching", "Getting Started", 4)]
    [InlineData("parallel-execution", "Parallel Execution", "Getting Started", 5)]
    [InlineData("error-handling", "Error Handling", "Getting Started", 5)]
    [InlineData("retry-with-backoff", "Retry with Backoff", "Getting Started", 3)]
    [InlineData("loop-processing", "Loop Processing", "Getting Started", 4)]
    [InlineData("csv-etl-pipeline", "CSV ETL Pipeline", "Data Processing", 4)]
    [InlineData("data-mapping-transform", "Data Mapping & Transform", "Data Processing", 3)]
    [InlineData("schema-validation", "Schema Validation", "Data Processing", 4)]
    [InlineData("order-processing-saga", "Order Processing Saga", "Order Management", 6)]
    [InlineData("express-order-flow", "Express Order Flow", "Order Management", 5)]
    [InlineData("order-with-approval", "Order with Approval", "Order Management", 6)]
    [InlineData("task-extraction-pipeline", "Task Extraction Pipeline", "AI & Agents", 5)]
    [InlineData("agent-triage-workflow", "Agent Triage Workflow", "AI & Agents", 4)]
    [InlineData("quick-transcript", "Quick Transcript", "Voice & Audio", 5)]
    [InlineData("meeting-notes", "Meeting Notes", "Voice & Audio", 7)]
    [InlineData("blog-from-interview", "Blog from Interview", "Voice & Audio", 10)]
    [InlineData("brain-dump-synthesis", "Brain Dump Synthesis", "Voice & Audio", 7)]
    [InlineData("podcast-transcript", "Podcast Transcript", "Voice & Audio", 6)]
    [InlineData("content-based-router", "Content-Based Router", "Integration Patterns", 4)]
    [InlineData("scatter-gather", "Scatter-Gather", "Integration Patterns", 5)]
    [InlineData("publish-subscribe", "Publish-Subscribe", "Integration Patterns", 5)]
    [InlineData("http-api-orchestration", "HTTP API Orchestration", "Integration Patterns", 5)]
    [InlineData("webhook-handler", "Webhook Handler", "Integration Patterns", 5)]
    public async Task GetTemplateAsync_LoadsCorrectly(string id, string expectedName, string expectedCategory, int expectedStepCount)
    {
        var template = await _library.GetTemplateAsync(id);

        template.Should().NotBeNull();
        template!.Name.Should().Be(expectedName);
        template.Category.Should().Be(expectedCategory);
        template.StepCount.Should().Be(expectedStepCount);
        template.Description.Should().NotBeNullOrEmpty();
        template.Tags.Should().NotBeEmpty();
        template.Definition.Should().NotBeNull();
        template.Definition.Steps.Should().NotBeEmpty();
        template.Definition.Name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AllTemplates_HaveUniqueIds()
    {
        var templates = await _library.GetTemplatesAsync();
        templates.Select(t => t.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task AllTemplates_HaveValidDifficulty()
    {
        var templates = await _library.GetTemplatesAsync();
        foreach (var summary in templates)
        {
            var template = await _library.GetTemplateAsync(summary.Id);
            template!.Difficulty.Should().BeOneOf(
                TemplateDifficulty.Beginner,
                TemplateDifficulty.Intermediate,
                TemplateDifficulty.Advanced);
        }
    }
}
