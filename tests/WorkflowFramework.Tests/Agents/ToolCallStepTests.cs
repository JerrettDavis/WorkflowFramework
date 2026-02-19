using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Agents;
using Xunit;

namespace WorkflowFramework.Tests.Agents;

public class ToolCallStepTests
{
    [Fact]
    public void Constructor_NullRegistry_Throws()
    {
        var act = () => new ToolCallStep(null!, "tool", "{}");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullToolName_Throws()
    {
        var act = () => new ToolCallStep(new ToolRegistry(), null!, "{}");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullTemplate_Throws()
    {
        var act = () => new ToolCallStep(new ToolRegistry(), "tool", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Name_Default_IncludesToolName()
    {
        var step = new ToolCallStep(new ToolRegistry(), "myTool", "{}");
        step.Name.Should().Be("ToolCall.myTool");
    }

    [Fact]
    public void Name_CustomStepName()
    {
        var step = new ToolCallStep(new ToolRegistry(), "myTool", "{}", "CustomName");
        step.Name.Should().Be("CustomName");
    }

    [Fact]
    public async Task ExecuteAsync_InvokesTool_StoresResult()
    {
        var registry = new ToolRegistry();
        var provider = Substitute.For<IToolProvider>();
        provider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(new List<ToolDefinition>
        {
            new() { Name = "tool1" }
        });
        provider.InvokeToolAsync("tool1", "{}", Arg.Any<CancellationToken>())
            .Returns(new ToolResult { Content = "result-val", IsError = false });
        registry.Register(provider);

        var step = new ToolCallStep(registry, "tool1", "{}");
        var context = new WorkflowContext();

        await step.ExecuteAsync(context);

        context.Properties["ToolCall.tool1.Result"].Should().Be("result-val");
        context.Properties["ToolCall.tool1.IsError"].Should().Be(false);
    }

    [Fact]
    public async Task ExecuteAsync_TemplateSubstitution()
    {
        var registry = new ToolRegistry();
        var provider = Substitute.For<IToolProvider>();
        provider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(new List<ToolDefinition>
        {
            new() { Name = "search" }
        });
        string? capturedArgs = null;
        provider.InvokeToolAsync("search", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedArgs = callInfo.ArgAt<string>(1);
                return new ToolResult { Content = "found" };
            });
        registry.Register(provider);

        var step = new ToolCallStep(registry, "search", "{\"query\": \"{userInput}\"}");
        var context = new WorkflowContext();
        context.Properties["userInput"] = "hello world";

        await step.ExecuteAsync(context);

        capturedArgs.Should().Be("{\"query\": \"hello world\"}");
    }

    [Fact]
    public async Task ExecuteAsync_ErrorResult()
    {
        var registry = new ToolRegistry();
        var provider = Substitute.For<IToolProvider>();
        provider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(new List<ToolDefinition>
        {
            new() { Name = "tool1" }
        });
        provider.InvokeToolAsync("tool1", "{}", Arg.Any<CancellationToken>())
            .Returns(new ToolResult { Content = "err", IsError = true });
        registry.Register(provider);

        var step = new ToolCallStep(registry, "tool1", "{}");
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        context.Properties["ToolCall.tool1.IsError"].Should().Be(true);
    }

    [Fact]
    public async Task ExecuteAsync_MissingTool_Throws()
    {
        var registry = new ToolRegistry();
        var step = new ToolCallStep(registry, "missing", "{}");
        var act = async () => await step.ExecuteAsync(new WorkflowContext());
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void SubstituteProperties_NoPlaceholders_ReturnsOriginal()
    {
        var result = ToolCallStep.SubstituteProperties("plain text", new Dictionary<string, object?>());
        result.Should().Be("plain text");
    }

    [Fact]
    public void SubstituteProperties_MissingKey_KeepsPlaceholder()
    {
        var result = ToolCallStep.SubstituteProperties("{missing}", new Dictionary<string, object?>());
        result.Should().Be("{missing}");
    }

    [Fact]
    public void SubstituteProperties_NullValue_KeepsPlaceholder()
    {
        var result = ToolCallStep.SubstituteProperties("{key}", new Dictionary<string, object?> { ["key"] = null });
        result.Should().Be("{key}");
    }

    [Fact]
    public void SubstituteProperties_MultiplePlaceholders()
    {
        var props = new Dictionary<string, object?> { ["a"] = "1", ["b"] = "2" };
        var result = ToolCallStep.SubstituteProperties("{a}-{b}", props);
        result.Should().Be("1-2");
    }
}
