using System.Text.Json;
using FluentAssertions;
using WorkflowFramework.Extensions.Agents;
using Xunit;

namespace WorkflowFramework.Tests.Agents;

public class CommandHookTests
{
    [Fact]
    public void Constructor_NullCommand_Throws()
    {
        var act = () => new CommandHook(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Matcher_ReturnsProvidedValue()
    {
        var hook = new CommandHook("cmd", matcher: "tool*");
        hook.Matcher.Should().Be("tool*");
    }

    [Fact]
    public void Matcher_DefaultIsNull()
    {
        var hook = new CommandHook("cmd");
        hook.Matcher.Should().BeNull();
    }

    [Fact]
    public void Timeout_DefaultIs30Seconds()
    {
        var hook = new CommandHook("cmd");
        hook.Timeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Timeout_CustomValue()
    {
        var hook = new CommandHook("cmd", timeout: TimeSpan.FromSeconds(60));
        hook.Timeout.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void HookContext_JsonRoundTrip()
    {
        var context = new HookContext
        {
            Event = AgentHookEvent.PreToolCall,
            StepName = "step1",
            ToolName = "myTool",
            ToolArgs = "{\"key\":\"val\"}"
        };

        var json = JsonSerializer.Serialize(context);
        var deserialized = JsonSerializer.Deserialize<HookContext>(json);

        deserialized.Should().NotBeNull();
        deserialized!.StepName.Should().Be("step1");
        deserialized.ToolName.Should().Be("myTool");
        deserialized.ToolArgs.Should().Be("{\"key\":\"val\"}");
    }

    [Fact]
    public void HookResult_JsonRoundTrip()
    {
        var result = HookResult.DenyResult("not allowed");
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<HookResult>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Decision.Should().Be(HookDecision.Deny);
        deserialized.Reason.Should().Be("not allowed");
    }

    [Fact]
    public void HookResult_AllowResult_JsonRoundTrip()
    {
        var result = HookResult.AllowResult("ok");
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<HookResult>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Decision.Should().Be(HookDecision.Allow);
        deserialized.Reason.Should().Be("ok");
    }

    [Fact]
    public void HookResult_ModifyResult_JsonRoundTrip()
    {
        var result = HookResult.ModifyResult("{\"modified\":true}", "changed args");
        var json = JsonSerializer.Serialize(result);
        var deserialized = JsonSerializer.Deserialize<HookResult>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Decision.Should().Be(HookDecision.Modify);
        deserialized.ModifiedArgs.Should().Be("{\"modified\":true}");
        deserialized.Reason.Should().Be("changed args");
    }
}
