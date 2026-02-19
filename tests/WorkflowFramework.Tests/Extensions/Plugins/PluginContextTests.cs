using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.Plugins;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Plugins;

public class PluginContextTests
{
    [Fact]
    public void Constructor_NullServices_Throws()
    {
        FluentActions.Invoking(() => new WorkflowPluginContext(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Services_ReturnsSameInstance()
    {
        var svc = new ServiceCollection();
        var ctx = new WorkflowPluginContext(svc);
        ctx.Services.Should().BeSameAs(svc);
    }

    [Fact]
    public void OnEvent_RegistersHook()
    {
        var ctx = new WorkflowPluginContext(new ServiceCollection());
        ctx.OnEvent("test", _ => Task.CompletedTask);
        ctx.GetEventHooks("test").Should().HaveCount(1);
    }

    [Fact]
    public void OnEvent_MultipleHandlers()
    {
        var ctx = new WorkflowPluginContext(new ServiceCollection());
        ctx.OnEvent("test", _ => Task.CompletedTask);
        ctx.OnEvent("test", _ => Task.CompletedTask);
        ctx.GetEventHooks("test").Should().HaveCount(2);
    }

    [Fact]
    public void GetEventHooks_NoHandlers_ReturnsEmpty()
    {
        var ctx = new WorkflowPluginContext(new ServiceCollection());
        ctx.GetEventHooks("nope").Should().BeEmpty();
    }
}
