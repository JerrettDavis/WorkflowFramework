using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Agents;

namespace WorkflowFramework.Tests.TinyBDD.Agents;

[Feature("Hook pipeline")]
public class HookPipelineTests : TinyBddTestBase
{
    public HookPipelineTests(ITestOutputHelper output) : base(output) { }

    [Scenario("All Allow hooks run and return Allow aggregate"), Fact]
    public async Task AllAllowHooksRun()
    {
        var h1 = Substitute.For<IAgentHook>();
        h1.Matcher.Returns((string?)null);
        h1.ExecuteAsync(Arg.Any<AgentHookEvent>(), Arg.Any<HookContext>(), Arg.Any<CancellationToken>())
            .Returns(HookResult.AllowResult("h1"));

        var h2 = Substitute.For<IAgentHook>();
        h2.Matcher.Returns((string?)null);
        h2.ExecuteAsync(Arg.Any<AgentHookEvent>(), Arg.Any<HookContext>(), Arg.Any<CancellationToken>())
            .Returns(HookResult.AllowResult("h2"));

        var pipeline = new HookPipeline(new[] { h1, h2 });
        var ctx = new HookContext { Event = AgentHookEvent.PreToolCall };
        var result = await pipeline.FireAsync(AgentHookEvent.PreToolCall, ctx);

        await Given("the result of firing a two-Allow hook pipeline", () => result)
            .Then("result decision is Allow", r =>
            {
                r.Decision.Should().Be(HookDecision.Allow);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Deny hook short-circuits remaining hooks"), Fact]
    public async Task DenyHookShortCircuits()
    {
        var executedAfterDeny = false;

        var denyHook = Substitute.For<IAgentHook>();
        denyHook.Matcher.Returns((string?)null);
        denyHook.ExecuteAsync(Arg.Any<AgentHookEvent>(), Arg.Any<HookContext>(), Arg.Any<CancellationToken>())
            .Returns(HookResult.DenyResult("denied"));

        var neverHook = Substitute.For<IAgentHook>();
        neverHook.Matcher.Returns((string?)null);
        neverHook.ExecuteAsync(Arg.Any<AgentHookEvent>(), Arg.Any<HookContext>(), Arg.Any<CancellationToken>())
            .Returns(ci => { executedAfterDeny = true; return Task.FromResult(HookResult.AllowResult()); });

        var pipeline = new HookPipeline(new[] { denyHook, neverHook });
        var ctx = new HookContext { Event = AgentHookEvent.PreToolCall };
        var result = await pipeline.FireAsync(AgentHookEvent.PreToolCall, ctx);

        await Given("result of deny-first pipeline and flag indicating if second hook ran", () => (result, executedAfterDeny))
            .Then("result is Deny and second hook was not called", state =>
            {
                state.result.Decision.Should().Be(HookDecision.Deny);
                state.executedAfterDeny.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Hooks with non-matching Matcher regex are skipped"), Fact]
    public async Task NonMatchingHooksAreSkipped()
    {
        var hookFired = false;

        var hook = Substitute.For<IAgentHook>();
        hook.Matcher.Returns("PostToolCall");
        hook.ExecuteAsync(Arg.Any<AgentHookEvent>(), Arg.Any<HookContext>(), Arg.Any<CancellationToken>())
            .Returns(ci => { hookFired = true; return Task.FromResult(HookResult.AllowResult()); });

        var pipeline = new HookPipeline(new[] { hook });
        var ctx = new HookContext { Event = AgentHookEvent.PreToolCall, StepName = "myStep" };
        var result = await pipeline.FireAsync(AgentHookEvent.PreToolCall, ctx);

        await Given("result when matcher does not match the fired event", () => (result, hookFired))
            .Then("the hook was not invoked and result is Allow", state =>
            {
                state.hookFired.Should().BeFalse();
                state.result.Decision.Should().Be(HookDecision.Allow);
                return true;
            })
            .AssertPassed();
    }
}
