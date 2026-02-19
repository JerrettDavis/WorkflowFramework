using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.AI;
using Xunit;

namespace WorkflowFramework.Tests.Agents;

public class PromptHookTests
{
    [Fact]
    public void Constructor_NullProvider_Throws()
    {
        var act = () => new PromptHook(null!, "template");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullTemplate_Throws()
    {
        var provider = Substitute.For<IAgentProvider>();
        var act = () => new PromptHook(provider, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_AllowResponse_ReturnsAllow()
    {
        var provider = Substitute.For<IAgentProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "This action is allowed" });

        var hook = new PromptHook(provider, "Should {event} be allowed for {toolName}?");
        var context = new HookContext { ToolName = "myTool", ToolArgs = "{}" };

        var result = await hook.ExecuteAsync(AgentHookEvent.PreToolCall, context);

        result.Decision.Should().Be(HookDecision.Allow);
    }

    [Fact]
    public async Task ExecuteAsync_DenyResponse_ReturnsDeny()
    {
        var provider = Substitute.For<IAgentProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "I must deny this request" });

        var hook = new PromptHook(provider, "Check {toolName}");
        var context = new HookContext { ToolName = "dangerous" };

        var result = await hook.ExecuteAsync(AgentHookEvent.PreToolCall, context);

        result.Decision.Should().Be(HookDecision.Deny);
    }

    [Fact]
    public async Task ExecuteAsync_BlockResponse_ReturnsDeny()
    {
        var provider = Substitute.For<IAgentProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "Block this action" });

        var hook = new PromptHook(provider, "template");
        var result = await hook.ExecuteAsync(AgentHookEvent.PreToolCall, new HookContext());

        result.Decision.Should().Be(HookDecision.Deny);
    }

    [Fact]
    public void Matcher_ReturnsProvidedValue()
    {
        var provider = Substitute.For<IAgentProvider>();
        var hook = new PromptHook(provider, "template", "match*");
        hook.Matcher.Should().Be("match*");
    }
}
