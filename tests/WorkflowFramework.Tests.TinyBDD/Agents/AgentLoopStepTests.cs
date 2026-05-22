using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.AI;

namespace WorkflowFramework.Tests.TinyBDD.Agents;

[Feature("Agent loop step")]
public class AgentLoopStepTests : TinyBddTestBase
{
    public AgentLoopStepTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Loop stops when LLM returns no tool calls"), Fact]
    public async Task LoopStopsWithNoToolCalls()
    {
        var provider = Substitute.For<IAgentProvider>();
        provider.Name.Returns("mock");
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "Done", ToolCalls = new List<ToolCall>() });

        var registry = new ToolRegistry();
        var options = new AgentLoopOptions { MaxIterations = 10, AutoCompact = false };
        var step = new AgentLoopStep(provider, registry, options);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        await Given("context after agent step with no-tool-call LLM", () => context)
            .Then("it completed after one iteration with expected response", ctx =>
            {
                ctx.Properties[step.Name + ".Iterations"].Should().Be(1);
                ctx.Properties[step.Name + ".Response"].Should().Be("Done");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Loop respects MaxIterations cap"), Fact]
    public async Task LoopRespectsMaxIterations()
    {
        var provider = Substitute.For<IAgentProvider>();
        provider.Name.Returns("mock");
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse
            {
                Content = "working",
                ToolCalls = new List<ToolCall> { new() { ToolName = "no-op", Arguments = "{}" } }
            });

        var toolProvider = Substitute.For<IToolProvider>();
        toolProvider.ListToolsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ToolDefinition> { new() { Name = "no-op" } });
        toolProvider.InvokeToolAsync("no-op", "{}", Arg.Any<CancellationToken>())
            .Returns(new ToolResult { Content = "ok" });

        var registry = new ToolRegistry();
        registry.Register(toolProvider);
        var options = new AgentLoopOptions { MaxIterations = 3, AutoCompact = false };
        var step = new AgentLoopStep(provider, registry, options);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        await Given("context after agent step capped at 3 iterations", () => context)
            .Then("iteration count does not exceed 3", ctx =>
            {
                ctx.Properties[step.Name + ".Iterations"].Should().Be(3);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Loop records last LLM response on context"), Fact]
    public async Task LoopRecordsLastResponse()
    {
        var provider = Substitute.For<IAgentProvider>();
        provider.Name.Returns("mock");
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "final answer", ToolCalls = new List<ToolCall>() });

        var registry = new ToolRegistry();
        var options = new AgentLoopOptions { MaxIterations = 5, AutoCompact = false };
        var step = new AgentLoopStep(provider, registry, options);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        await Given("context after agent step execution", () => context)
            .Then("context holds the last response text", ctx =>
            {
                ctx.Properties[step.Name + ".Response"].Should().Be("final answer");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Hook Deny on tool call records denied result without invoking tool"), Fact]
    public async Task HookDenyBlocksToolCall()
    {
        var provider = Substitute.For<IAgentProvider>();
        provider.Name.Returns("mock");
        var callCount = 0;
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                callCount++;
                if (callCount == 1)
                    return Task.FromResult(new LlmResponse
                    {
                        Content = "calling tool",
                        ToolCalls = new List<ToolCall> { new() { ToolName = "blocked-tool", Arguments = "{}" } }
                    });
                return Task.FromResult(new LlmResponse { Content = "finished", ToolCalls = new List<ToolCall>() });
            });

        var denyHook = Substitute.For<IAgentHook>();
        denyHook.Matcher.Returns((string?)null);
        denyHook.ExecuteAsync(AgentHookEvent.PreToolCall, Arg.Any<HookContext>(), Arg.Any<CancellationToken>())
            .Returns(HookResult.DenyResult("blocked"));
        denyHook.ExecuteAsync(Arg.Is<AgentHookEvent>(e => e != AgentHookEvent.PreToolCall), Arg.Any<HookContext>(), Arg.Any<CancellationToken>())
            .Returns(HookResult.AllowResult());

        var registry = new ToolRegistry();
        var hooks = new HookPipeline(new[] { denyHook });
        var options = new AgentLoopOptions { MaxIterations = 5, AutoCompact = false, Hooks = hooks };
        var step = new AgentLoopStep(provider, registry, options);
        var context = new WorkflowContext();
        await step.ExecuteAsync(context);

        await Given("context after agent step with a deny hook blocking the tool", () => context)
            .Then("loop completed with finished response", ctx =>
            {
                ctx.Properties[step.Name + ".Response"].Should().Be("finished");
                return true;
            })
            .AssertPassed();
    }
}
