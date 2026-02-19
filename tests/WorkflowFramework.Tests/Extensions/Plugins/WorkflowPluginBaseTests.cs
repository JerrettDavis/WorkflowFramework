using FluentAssertions;
using WorkflowFramework.Extensions.Plugins;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Plugins;

public class WorkflowPluginBaseTests
{
    private class MinimalPlugin : WorkflowPluginBase
    {
        public override string Name => "Minimal";
        public override void Configure(IWorkflowPluginContext context) { }
    }

    [Fact]
    public void Version_DefaultsTo1_0_0()
    {
        var p = new MinimalPlugin();
        p.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void Dependencies_DefaultsToEmpty()
    {
        var p = new MinimalPlugin();
        p.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_DefaultDoesNothing()
    {
        var p = new MinimalPlugin();
        await p.InitializeAsync();
    }

    [Fact]
    public async Task StartAsync_DefaultDoesNothing()
    {
        var p = new MinimalPlugin();
        await p.StartAsync();
    }

    [Fact]
    public async Task StopAsync_DefaultDoesNothing()
    {
        var p = new MinimalPlugin();
        await p.StopAsync();
    }

    [Fact]
    public async Task DisposeAsync_DefaultDoesNothing()
    {
        var p = new MinimalPlugin();
        await p.DisposeAsync();
    }

    [Fact]
    public void Name_ReturnsOverriddenName()
    {
        var p = new MinimalPlugin();
        p.Name.Should().Be("Minimal");
    }
}
