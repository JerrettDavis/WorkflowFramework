using FluentAssertions;
using WorkflowFramework.Extensions.Plugins;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Plugins;

public class PluginManifestTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var m = new PluginManifest();
        m.Name.Should().BeEmpty();
        m.Version.Should().Be("1.0.0");
        m.Description.Should().BeEmpty();
        m.Author.Should().BeEmpty();
        m.Capabilities.Should().BeEmpty();
        m.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var m = new PluginManifest
        {
            Name = "MyPlugin",
            Version = "2.0.0",
            Description = "A plugin",
            Author = "Me",
            Capabilities = new List<string> { "logging", "metrics" },
            Dependencies = new List<string> { "Core" }
        };
        m.Name.Should().Be("MyPlugin");
        m.Version.Should().Be("2.0.0");
        m.Capabilities.Should().HaveCount(2);
        m.Dependencies.Should().ContainSingle().Which.Should().Be("Core");
    }
}
