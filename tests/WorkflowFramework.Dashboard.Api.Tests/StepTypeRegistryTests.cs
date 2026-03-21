using Xunit;
using FluentAssertions;
using System.Text.Json;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Services;

namespace WorkflowFramework.Dashboard.Api.Tests;

public class StepTypeRegistryTests
{
    [Fact]
    public void CreateDefault_RegistersAllExpectedStepTypes()
    {
        var registry = StepTypeRegistry.CreateDefault();

        registry.All.Should().HaveCountGreaterThanOrEqualTo(28);
    }

    [Theory]
    [InlineData("Action", "Core")]
    [InlineData("Conditional", "Core")]
    [InlineData("Parallel", "Core")]
    [InlineData("ForEach", "Core")]
    [InlineData("Retry", "Core")]
    [InlineData("Saga", "Core")]
    [InlineData("ContentBasedRouter", "Integration")]
    [InlineData("Splitter", "Integration")]
    [InlineData("AgentLoopStep", "AI/Agents")]
    [InlineData("LlmCallStep", "AI/Agents")]
    [InlineData("DataMapStep", "Data")]
    [InlineData("HttpStep", "HTTP")]
    [InlineData("PublishEventStep", "Events")]
    [InlineData("HumanTaskStep", "Human")]
    [InlineData("ApprovalStep", "Human")]
    public void CreateDefault_ContainsStepType(string type, string expectedCategory)
    {
        var registry = StepTypeRegistry.CreateDefault();
        var info = registry.Get(type);

        info.Should().NotBeNull();
        info!.Category.Should().Be(expectedCategory);
        info.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Get_ReturnsNullForUnknown()
    {
        var registry = StepTypeRegistry.CreateDefault();
        registry.Get("NonExistent").Should().BeNull();
    }

    [Fact]
    public void Get_IsCaseInsensitive()
    {
        var registry = StepTypeRegistry.CreateDefault();
        registry.Get("action").Should().NotBeNull();
        registry.Get("ACTION").Should().NotBeNull();
    }

    [Fact]
    public void Register_AddsCustomStepType()
    {
        var registry = new StepTypeRegistry();
        registry.Register(new StepTypeInfo { Type = "Custom", Name = "Custom", Category = "Custom", Description = "A custom step" });

        registry.All.Should().HaveCount(1);
        registry.Get("Custom").Should().NotBeNull();
    }

    [Fact]
    public void CreateDefault_UsesCanonicalSchemaKeys_ForStructuredCoreSteps()
    {
        var registry = StepTypeRegistry.CreateDefault();

        var timeoutProperties = GetPropertyNames(registry.Get("Timeout"));
        timeoutProperties.Should().Contain("timeoutSeconds");
        timeoutProperties.Should().NotContain("durationMs");

        var delayProperties = GetPropertyNames(registry.Get("Delay"));
        delayProperties.Should().Contain("delaySeconds");
        delayProperties.Should().NotContain("durationMs");

        var subWorkflowProperties = GetPropertyNames(registry.Get("SubWorkflow"));
        subWorkflowProperties.Should().Contain("subWorkflowName");
        subWorkflowProperties.Should().NotContain("workflowName");

        var conditionalProperties = GetPropertyNames(registry.Get("Conditional"));
        conditionalProperties.Should().Contain("expression");
        conditionalProperties.Should().NotContain("thenStep");
        conditionalProperties.Should().NotContain("elseStep");

        var retryProperties = GetPropertyNames(registry.Get("Retry"));
        retryProperties.Should().Contain("maxAttempts");
        retryProperties.Should().NotContain("delayMs");
        retryProperties.Should().NotContain("backoffMultiplier");

        var httpProperties = GetPropertyNames(registry.Get("HttpStep"));
        httpProperties.Should().Contain(["url", "method", "headers", "body", "contentType"]);
        httpProperties.Should().NotContain("timeoutMs");
    }

    [Fact]
    public void CreateDefault_UsesSharedAiProviderMetadata_ForAiStepSchemas()
    {
        var registry = StepTypeRegistry.CreateDefault();

        var llmSchema = registry.Get("LlmCallStep")!.ConfigSchema!.Value;
        var providerProperty = llmSchema.GetProperty("properties").GetProperty("provider");
        providerProperty.GetProperty("options").EnumerateArray().Select(option => option.GetString())
            .Should().ContainInOrder("ollama", "openai", "anthropic", "huggingface");

        var modelProperty = llmSchema.GetProperty("properties").GetProperty("model");
        var optionGroups = modelProperty.GetProperty("optionGroups");
        optionGroups.GetProperty("openai").EnumerateArray().Select(option => option.GetString())
            .Should().Contain(["gpt-4o", "gpt-4o-mini"]);
        optionGroups.GetProperty("anthropic").EnumerateArray().Select(option => option.GetString())
            .Should().Contain("claude-sonnet-4-20250514");

        GetPropertyNames(registry.Get("AgentLoopStep")).Should().NotContain("tools");
        GetPropertyNames(registry.Get("AgentPlanStep")).Should().NotContain("maxSteps");
    }

    private static IReadOnlyList<string> GetPropertyNames(StepTypeInfo? stepType)
    {
        stepType.Should().NotBeNull();
        stepType!.ConfigSchema.Should().NotBeNull();

        var properties = stepType.ConfigSchema!.Value.GetProperty("properties");
        return properties.EnumerateObject().Select(p => p.Name).ToList();
    }
}
