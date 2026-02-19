using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Agents;
using Xunit;

namespace WorkflowFramework.Tests.Agents;

public class ToolRegistryTests
{
    [Fact]
    public void Register_NullProvider_Throws()
    {
        var registry = new ToolRegistry();
        var act = () => registry.Register(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Register_AddsProvider()
    {
        var registry = new ToolRegistry();
        var provider = Substitute.For<IToolProvider>();
        registry.Register(provider);
        registry.Providers.Should().HaveCount(1);
    }

    [Fact]
    public void Providers_ReturnsSnapshot()
    {
        var registry = new ToolRegistry();
        var provider = Substitute.For<IToolProvider>();
        registry.Register(provider);
        var snap = registry.Providers;
        snap.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListAllToolsAsync_ReturnsToolsFromAllProviders()
    {
        var registry = new ToolRegistry();
        var p1 = Substitute.For<IToolProvider>();
        p1.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(new List<ToolDefinition>
        {
            new() { Name = "tool1", Description = "desc1" }
        });
        var p2 = Substitute.For<IToolProvider>();
        p2.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(new List<ToolDefinition>
        {
            new() { Name = "tool2", Description = "desc2" }
        });

        registry.Register(p1);
        registry.Register(p2);

        var tools = await registry.ListAllToolsAsync();
        tools.Should().HaveCount(2);
        tools.Select(t => t.Name).Should().Contain("tool1").And.Contain("tool2");
    }

    [Fact]
    public async Task ListAllToolsAsync_DeduplicatesByName_LastRegisteredWins()
    {
        var registry = new ToolRegistry();
        var p1 = Substitute.For<IToolProvider>();
        p1.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(new List<ToolDefinition>
        {
            new() { Name = "tool1", Description = "first" }
        });
        var p2 = Substitute.For<IToolProvider>();
        p2.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(new List<ToolDefinition>
        {
            new() { Name = "tool1", Description = "second" }
        });

        registry.Register(p1);
        registry.Register(p2);

        var tools = await registry.ListAllToolsAsync();
        tools.Should().HaveCount(1);
        tools[0].Description.Should().Be("second");
    }

    [Fact]
    public async Task ListAllToolsAsync_EmptyRegistry_ReturnsEmpty()
    {
        var registry = new ToolRegistry();
        var tools = await registry.ListAllToolsAsync();
        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeAsync_NullToolName_Throws()
    {
        var registry = new ToolRegistry();
        var act = async () => await registry.InvokeAsync(null!, "{}");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task InvokeAsync_NullArgs_Throws()
    {
        var registry = new ToolRegistry();
        var act = async () => await registry.InvokeAsync("tool", null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task InvokeAsync_ToolNotFound_Throws()
    {
        var registry = new ToolRegistry();
        var provider = Substitute.For<IToolProvider>();
        provider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(new List<ToolDefinition>());
        registry.Register(provider);

        var act = async () => await registry.InvokeAsync("missing", "{}");
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*missing*not found*");
    }

    [Fact]
    public async Task InvokeAsync_ToolNotFound_EmptyRegistry_Throws()
    {
        var registry = new ToolRegistry();
        var act = async () => await registry.InvokeAsync("missing", "{}");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task InvokeAsync_InvokesCorrectProvider()
    {
        var registry = new ToolRegistry();
        var provider = Substitute.For<IToolProvider>();
        provider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(new List<ToolDefinition>
        {
            new() { Name = "myTool" }
        });
        provider.InvokeToolAsync("myTool", "{\"x\":1}", Arg.Any<CancellationToken>())
            .Returns(new ToolResult { Content = "result" });
        registry.Register(provider);

        var result = await registry.InvokeAsync("myTool", "{\"x\":1}");
        result.Content.Should().Be("result");
    }

    [Fact]
    public async Task InvokeAsync_LastRegisteredWins()
    {
        var registry = new ToolRegistry();
        var p1 = Substitute.For<IToolProvider>();
        p1.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(new List<ToolDefinition>
        {
            new() { Name = "tool1" }
        });
        p1.InvokeToolAsync("tool1", "{}", Arg.Any<CancellationToken>())
            .Returns(new ToolResult { Content = "from-p1" });

        var p2 = Substitute.For<IToolProvider>();
        p2.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(new List<ToolDefinition>
        {
            new() { Name = "tool1" }
        });
        p2.InvokeToolAsync("tool1", "{}", Arg.Any<CancellationToken>())
            .Returns(new ToolResult { Content = "from-p2" });

        registry.Register(p1);
        registry.Register(p2);

        var result = await registry.InvokeAsync("tool1", "{}");
        result.Content.Should().Be("from-p2");
    }

    [Fact]
    public async Task InvokeAsync_WithCancellation()
    {
        var registry = new ToolRegistry();
        var cts = new CancellationTokenSource();
        var provider = Substitute.For<IToolProvider>();
        provider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(new List<ToolDefinition>
        {
            new() { Name = "tool1" }
        });
        provider.InvokeToolAsync("tool1", "{}", Arg.Any<CancellationToken>())
            .Returns(new ToolResult { Content = "ok" });
        registry.Register(provider);

        var result = await registry.InvokeAsync("tool1", "{}", cts.Token);
        result.Content.Should().Be("ok");
    }
}
