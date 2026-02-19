using FluentAssertions;
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
}
