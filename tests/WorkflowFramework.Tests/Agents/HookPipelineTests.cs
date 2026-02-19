using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Agents;
using Xunit;

namespace WorkflowFramework.Tests.Agents;

public class HookPipelineTests
{
    [Fact]
    public void Constructor_Default_Empty()
    {
        var pipeline = new HookPipeline();
        pipeline.Hooks.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithHooks()
    {
        var hook = Substitute.For<IAgentHook>();
        var pipeline = new HookPipeline(new[] { hook });
        pipeline.Hooks.Should().HaveCount(1);
    }

    [Fact]
    public void Constructor_NullHooks_Throws()
    {
        var act = () => new HookPipeline(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Add_NullHook_Throws()
    {
        var pipeline = new HookPipeline();
        var act = () => pipeline.Add(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Add_AddsHook()
    {
        var pipeline = new HookPipeline();
        var hook = Substitute.For<IAgentHook>();
        pipeline.Add(hook);
        pipeline.Hooks.Should().HaveCount(1);
    }

    [Fact]
    public async Task FireAsync_CallsMatchingHooksInOrder()
    {
        var order = new List<int>();
        var hook1 = Substitute.For<IAgentHook>();
        hook1.Matcher.Returns((string?)null); // matches all
        hook1.ExecuteAsync(Arg.Any<AgentHookEvent>(), Arg.Any<HookContext>(), Arg.Any<CancellationToken>())
            .Returns(_ => { order.Add(1); return HookResult.AllowResult(); });
        var hook2 = Substitute.For<IAgentHook>();
        hook2.Matcher.Returns((string?)null);
        hook2.ExecuteAsync(Arg.Any<AgentHookEvent>(), Arg.Any<HookContext>(), Arg.Any<CancellationToken>())
            .Returns(_ => { order.Add(2); return HookResult.AllowResult(); });

        var pipeline = new HookPipeline(new[] { hook1, hook2 });
        var ctx = new HookContext { Event = AgentHookEvent.PreToolCall, StepName = "test" };
        await pipeline.FireAsync(AgentHookEvent.PreToolCall, ctx);

        order.Should().Equal(1, 2);
    }

    [Fact]
    public async Task FireAsync_DenyStopsExecution()
    {
        var hook1 = Substitute.For<IAgentHook>();
        hook1.Matcher.Returns((string?)null);
        hook1.ExecuteAsync(Arg.Any<AgentHookEvent>(), Arg.Any<HookContext>(), Arg.Any<CancellationToken>())
            .Returns(HookResult.DenyResult("blocked"));
        var hook2 = Substitute.For<IAgentHook>();
        hook2.Matcher.Returns((string?)null);
        hook2.ExecuteAsync(Arg.Any<AgentHookEvent>(), Arg.Any<HookContext>(), Arg.Any<CancellationToken>())
            .Returns(HookResult.AllowResult());

        var pipeline = new HookPipeline(new[] { hook1, hook2 });
        var result = await pipeline.FireAsync(AgentHookEvent.PreToolCall, new HookContext());

        result.Decision.Should().Be(HookDecision.Deny);
        result.Reason.Should().Be("blocked");
        await hook2.DidNotReceive().ExecuteAsync(Arg.Any<AgentHookEvent>(), Arg.Any<HookContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FireAsync_ModifyUpdatesArgs()
    {
        var hook = Substitute.For<IAgentHook>();
        hook.Matcher.Returns((string?)null);
        hook.ExecuteAsync(Arg.Any<AgentHookEvent>(), Arg.Any<HookContext>(), Arg.Any<CancellationToken>())
            .Returns(HookResult.ModifyResult("{\"modified\":true}", "rewritten"));

        var pipeline = new HookPipeline(new[] { hook });
        var ctx = new HookContext { ToolArgs = "{\"original\":true}" };
        var result = await pipeline.FireAsync(AgentHookEvent.PreToolCall, ctx);

        result.Decision.Should().Be(HookDecision.Modify);
        result.ModifiedArgs.Should().Be("{\"modified\":true}");
        ctx.ToolArgs.Should().Be("{\"modified\":true}");
    }

    [Fact]
    public async Task FireAsync_EmptyPipeline_ReturnsAllow()
    {
        var pipeline = new HookPipeline();
        var result = await pipeline.FireAsync(AgentHookEvent.PreToolCall, new HookContext());
        result.Decision.Should().Be(HookDecision.Allow);
    }

    [Fact]
    public async Task FireAsync_MatcherFiltersHooks()
    {
        var hook = Substitute.For<IAgentHook>();
        hook.Matcher.Returns("DoesNotMatch");
        hook.ExecuteAsync(Arg.Any<AgentHookEvent>(), Arg.Any<HookContext>(), Arg.Any<CancellationToken>())
            .Returns(HookResult.DenyResult());

        var pipeline = new HookPipeline(new[] { hook });
        var result = await pipeline.FireAsync(AgentHookEvent.PreToolCall, new HookContext { StepName = "MyStep" });

        result.Decision.Should().Be(HookDecision.Allow);
    }

    [Fact]
    public async Task FireAsync_MatcherMatchesHook()
    {
        var hook = Substitute.For<IAgentHook>();
        hook.Matcher.Returns(".*MyStep.*");
        hook.ExecuteAsync(Arg.Any<AgentHookEvent>(), Arg.Any<HookContext>(), Arg.Any<CancellationToken>())
            .Returns(HookResult.AllowResult("matched"));

        var pipeline = new HookPipeline(new[] { hook });
        var result = await pipeline.FireAsync(AgentHookEvent.PreToolCall, new HookContext { StepName = "MyStep" });

        result.Reason.Should().Be("matched");
    }

    [Fact]
    public async Task FireAsync_PropagatesExceptionFromHook()
    {
        var hook = Substitute.For<IAgentHook>();
        hook.Matcher.Returns((string?)null);
        hook.ExecuteAsync(Arg.Any<AgentHookEvent>(), Arg.Any<HookContext>(), Arg.Any<CancellationToken>())
            .Returns<HookResult>(_ => throw new InvalidOperationException("hook failed"));

        var pipeline = new HookPipeline(new[] { hook });
        var act = async () => await pipeline.FireAsync(AgentHookEvent.PreToolCall, new HookContext());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("hook failed");
    }
}
