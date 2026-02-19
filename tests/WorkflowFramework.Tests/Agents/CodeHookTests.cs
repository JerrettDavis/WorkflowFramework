using FluentAssertions;
using WorkflowFramework.Extensions.Agents;
using Xunit;

namespace WorkflowFramework.Tests.Agents;

public class CodeHookTests
{
    [Fact]
    public void Constructor_NullHandler_Throws()
    {
        var act = () => new CodeHook(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Matcher_ReturnsProvidedValue()
    {
        var hook = new CodeHook(_ => Task.FromResult(HookResult.AllowResult()), "test*");
        hook.Matcher.Should().Be("test*");
    }

    [Fact]
    public void Matcher_DefaultIsNull()
    {
        var hook = new CodeHook(_ => Task.FromResult(HookResult.AllowResult()));
        hook.Matcher.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_InvokesDelegate()
    {
        var called = false;
        var hook = new CodeHook(ctx =>
        {
            called = true;
            return Task.FromResult(HookResult.AllowResult("ok"));
        });

        var result = await hook.ExecuteAsync(AgentHookEvent.PreToolCall, new HookContext());

        called.Should().BeTrue();
        result.Decision.Should().Be(HookDecision.Allow);
        result.Reason.Should().Be("ok");
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsDenyResult()
    {
        var hook = new CodeHook(_ => Task.FromResult(HookResult.DenyResult("blocked")));
        var result = await hook.ExecuteAsync(AgentHookEvent.PreToolCall, new HookContext());
        result.Decision.Should().Be(HookDecision.Deny);
        result.Reason.Should().Be("blocked");
    }
}
