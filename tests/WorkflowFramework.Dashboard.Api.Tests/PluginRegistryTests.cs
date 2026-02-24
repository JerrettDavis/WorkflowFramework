using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Plugins;
using WorkflowFramework.Dashboard.Api.Plugins.BuiltInPlugins;
using Xunit;

namespace WorkflowFramework.Dashboard.Api.Tests;

public class PluginRegistryTests
{
    [Fact]
    public void Register_AddsPlugin()
    {
        var registry = new PluginRegistry();
        var plugin = new EmailStepPlugin();

        registry.Register(plugin);

        registry.Plugins.Should().HaveCount(1);
        registry.Plugins[0].Id.Should().Be("builtin.email");
    }

    [Fact]
    public void CreateStep_KnownType_ReturnsStep()
    {
        var registry = new PluginRegistry();
        registry.Register(new EmailStepPlugin());

        var step = registry.CreateStep("SendEmail", "myEmail", new Dictionary<string, string>
        {
            ["to"] = "test@example.com",
            ["subject"] = "Hello"
        });

        step.Should().NotBeNull();
        step!.Name.Should().Be("myEmail");
    }

    [Fact]
    public void CreateStep_UnknownType_ReturnsNull()
    {
        var registry = new PluginRegistry();
        registry.Register(new EmailStepPlugin());

        var step = registry.CreateStep("UnknownStep", "test", null);

        step.Should().BeNull();
    }

    [Fact]
    public void GetAllStepTypes_ReturnsPluginTypes()
    {
        var registry = new PluginRegistry();
        registry.Register(new EmailStepPlugin());

        var types = registry.GetAllStepTypes();

        types.Should().HaveCount(1);
        types[0].Type.Should().Be("SendEmail");
        types[0].Category.Should().Be("Communication");
    }

    [Fact]
    public async Task EmailPlugin_CreateStep_Executes()
    {
        var plugin = new EmailStepPlugin();
        var step = plugin.CreateStep("SendEmail", "emailStep", new Dictionary<string, string>
        {
            ["to"] = "user@test.com",
            ["subject"] = "Test Subject"
        });

        step.Should().NotBeNull();

        var context = new WorkflowContext();
        await step!.ExecuteAsync(context);

        context.Properties["emailStep.Output"].Should().Be("Email sent to user@test.com: Test Subject");
    }
}
