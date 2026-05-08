using FluentAssertions;
using WorkflowFramework.Triggers;
using Xunit;

namespace WorkflowFramework.Tests.Triggers;

public class TriggerDefinitionTests
{
    [Fact]
    public void Defaults_AreInitialized()
    {
        var definition = new TriggerDefinition();

        definition.Id.Should().NotBeNullOrWhiteSpace();
        definition.Type.Should().BeEmpty();
        definition.Name.Should().BeNull();
        definition.Enabled.Should().BeTrue();
        definition.Configuration.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void Properties_CanBeAssigned()
    {
        var definition = new TriggerDefinition
        {
            Id = "trigger-1",
            Type = "manual",
            Name = "Manual trigger",
            Enabled = false,
            Configuration = new Dictionary<string, string>
            {
                ["inputSchema"] = "{}"
            }
        };

        definition.Id.Should().Be("trigger-1");
        definition.Type.Should().Be("manual");
        definition.Name.Should().Be("Manual trigger");
        definition.Enabled.Should().BeFalse();
        definition.Configuration.Should().ContainKey("inputSchema").WhoseValue.Should().Be("{}");
    }
}
