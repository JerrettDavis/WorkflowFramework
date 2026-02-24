using Xunit;
using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Services;

namespace WorkflowFramework.Dashboard.Api.Tests;

public class TriggerTypeRegistryTests
{
    [Fact]
    public void CreateDefault_RegistersAllSixTypes()
    {
        var registry = TriggerTypeRegistry.CreateDefault();
        registry.GetAll().Should().HaveCount(6);
    }

    [Theory]
    [InlineData("manual", "Basic")]
    [InlineData("schedule", "Time")]
    [InlineData("webhook", "HTTP")]
    [InlineData("filewatch", "I/O")]
    [InlineData("audio", "I/O")]
    [InlineData("queue", "Integration")]
    public void CreateDefault_HasExpectedTypeAndCategory(string type, string category)
    {
        var registry = TriggerTypeRegistry.CreateDefault();
        var info = registry.GetByType(type);
        info.Should().NotBeNull();
        info!.Category.Should().Be(category);
        info.DisplayName.Should().NotBeNullOrWhiteSpace();
        info.Description.Should().NotBeNullOrWhiteSpace();
        info.Icon.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void AllTypes_HaveConfigSchema()
    {
        var registry = TriggerTypeRegistry.CreateDefault();
        foreach (var t in registry.GetAll())
        {
            t.ConfigSchema.Should().NotBeNullOrWhiteSpace($"type '{t.Type}' should have a config schema");
        }
    }

    [Fact]
    public void GetByType_UnknownType_ReturnsNull()
    {
        var registry = TriggerTypeRegistry.CreateDefault();
        registry.GetByType("nonexistent").Should().BeNull();
    }

    [Fact]
    public void Register_AddsCustomType()
    {
        var registry = TriggerTypeRegistry.CreateDefault();
        var initial = registry.GetAll().Count;
        registry.Register(new TriggerTypeInfoDto { Type = "custom", DisplayName = "Custom" });
        registry.GetAll().Should().HaveCount(initial + 1);
        registry.GetByType("custom").Should().NotBeNull();
    }
}

public class TriggerModelTests
{
    [Fact]
    public void SavedWorkflowDefinition_HasTriggersProperty()
    {
        var saved = new SavedWorkflowDefinition();
        saved.Triggers.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void TriggerDefinitionDto_HasDefaults()
    {
        var dto = new TriggerDefinitionDto();
        dto.Id.Should().NotBeNullOrWhiteSpace();
        dto.Enabled.Should().BeTrue();
        dto.Configuration.Should().NotBeNull();
    }

    [Fact]
    public void SetTriggersRequest_RoundTrip()
    {
        var request = new SetTriggersRequest
        {
            Triggers =
            [
                new TriggerDefinitionDto { Type = "schedule", Name = "Every 5 min", Configuration = new() { ["cronExpression"] = "*/5 * * * *" } },
                new TriggerDefinitionDto { Type = "manual", Enabled = false }
            ]
        };
        request.Triggers.Should().HaveCount(2);
        request.Triggers[0].Configuration["cronExpression"].Should().Be("*/5 * * * *");
        request.Triggers[1].Enabled.Should().BeFalse();
    }
}
