using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.AI;
using Xunit;

namespace WorkflowFramework.Tests.Agents;

public class AgentLoopStepTests
{
    private static ToolRegistry CreateRegistryWithTool(string name, string result)
    {
        var registry = new ToolRegistry();
        var provider = Substitute.For<IToolProvider>();
        provider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(new List<ToolDefinition>
        {
            new() { Name = name, Description = "test" }
        });
        provider.InvokeToolAsync(name, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ToolResult { Content = result });
        registry.Register(provider);
        return registry;
    }

    [Fact]
    public void Constructor_NullProvider_Throws()
    {
        var act = () => new AgentLoopStep(null!, new ToolRegistry(), new AgentLoopOptions());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullRegistry_Throws()
    {
        var provider = Substitute.For<IAgentProvider>();
        var act = () => new AgentLoopStep(provider, null!, new AgentLoopOptions());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var provider = Substitute.For<IAgentProvider>();
        var act = () => new AgentLoopStep(provider, new ToolRegistry(), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Name_Default_IsAgentLoop()
    {
        var provider = Substitute.For<IAgentProvider>();
        var step = new AgentLoopStep(provider, new ToolRegistry(), new AgentLoopOptions());
        step.Name.Should().Be("AgentLoop");
    }

    [Fact]
    public void Name_CustomStepName()
    {
        var provider = Substitute.For<IAgentProvider>();
        var step = new AgentLoopStep(provider, new ToolRegistry(), new AgentLoopOptions { StepName = "MyLoop" });
        step.Name.Should().Be("MyLoop");
    }

    [Fact]
    public async Task ExecuteAsync_NoToolCalls_SingleIteration()
    {
        var provider = Substitute.For<IAgentProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "Hello!" });

        var registry = new ToolRegistry();
        var step = new AgentLoopStep(provider, registry, new AgentLoopOptions());
        var context = new WorkflowContext();

        await step.ExecuteAsync(context);

        context.Properties["AgentLoop.Response"].Should().Be("Hello!");
        context.Properties["AgentLoop.Iterations"].Should().Be(1);
        ((List<ToolResult>)context.Properties["AgentLoop.ToolResults"]!).Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ToolCallsThenContent_TwoIterations()
    {
        var provider = Substitute.For<IAgentProvider>();
        var callCount = 0;
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                    return new LlmResponse
                    {
                        Content = "",
                        ToolCalls = new List<ToolCall> { new() { ToolName = "search", Arguments = "{\"q\":\"test\"}" } }
                    };
                return new LlmResponse { Content = "Final answer" };
            });

        var registry = CreateRegistryWithTool("search", "search result");
        var step = new AgentLoopStep(provider, registry, new AgentLoopOptions());
        var context = new WorkflowContext();

        await step.ExecuteAsync(context);

        context.Properties["AgentLoop.Response"].Should().Be("Final answer");
        context.Properties["AgentLoop.Iterations"].Should().Be(2);
        var results = (List<ToolResult>)context.Properties["AgentLoop.ToolResults"]!;
        results.Should().HaveCount(1);
        results[0].Content.Should().Be("search result");
    }

    [Fact]
    public async Task ExecuteAsync_MaxIterationsCap()
    {
        var provider = Substitute.For<IAgentProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse
            {
                Content = "looping",
                ToolCalls = new List<ToolCall> { new() { ToolName = "tool1", Arguments = "{}" } }
            });

        var registry = CreateRegistryWithTool("tool1", "result");
        var step = new AgentLoopStep(provider, registry, new AgentLoopOptions { MaxIterations = 3 });
        var context = new WorkflowContext();

        await step.ExecuteAsync(context);

        context.Properties["AgentLoop.Iterations"].Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_HookFiresOnToolCall()
    {
        var provider = Substitute.For<IAgentProvider>();
        var callCount = 0;
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                    return new LlmResponse
                    {
                        ToolCalls = new List<ToolCall> { new() { ToolName = "t1", Arguments = "{}" } }
                    };
                return new LlmResponse { Content = "done" };
            });

        var hook = Substitute.For<IAgentHook>();
        hook.Matcher.Returns((string?)null);
        hook.ExecuteAsync(Arg.Any<AgentHookEvent>(), Arg.Any<HookContext>(), Arg.Any<CancellationToken>())
            .Returns(HookResult.AllowResult());

        var registry = CreateRegistryWithTool("t1", "r1");
        var hookPipeline = new HookPipeline(new[] { hook });
        var step = new AgentLoopStep(provider, registry, new AgentLoopOptions { Hooks = hookPipeline });

        await step.ExecuteAsync(new WorkflowContext());

        await hook.Received().ExecuteAsync(AgentHookEvent.PreToolCall, Arg.Any<HookContext>(), Arg.Any<CancellationToken>());
        await hook.Received().ExecuteAsync(AgentHookEvent.PostToolCall, Arg.Any<HookContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_HookDeniesTool()
    {
        var provider = Substitute.For<IAgentProvider>();
        var callCount = 0;
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                    return new LlmResponse
                    {
                        ToolCalls = new List<ToolCall> { new() { ToolName = "dangerous", Arguments = "{}" } }
                    };
                return new LlmResponse { Content = "ok" };
            });

        var hook = Substitute.For<IAgentHook>();
        hook.Matcher.Returns((string?)null);
        hook.ExecuteAsync(AgentHookEvent.PreToolCall, Arg.Any<HookContext>(), Arg.Any<CancellationToken>())
            .Returns(HookResult.DenyResult("not allowed"));
        hook.ExecuteAsync(Arg.Is<AgentHookEvent>(e => e != AgentHookEvent.PreToolCall), Arg.Any<HookContext>(), Arg.Any<CancellationToken>())
            .Returns(HookResult.AllowResult());

        var registry = CreateRegistryWithTool("dangerous", "r1");
        var step = new AgentLoopStep(provider, registry, new AgentLoopOptions { Hooks = new HookPipeline(new[] { hook }) });

        await step.ExecuteAsync(new WorkflowContext());

        // Tool should NOT have been invoked on registry (denied by hook)
        var toolProvider = registry.Providers[0];
        await toolProvider.DidNotReceive().InvokeToolAsync("dangerous", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ContextSourcesInjected()
    {
        var provider = Substitute.For<IAgentProvider>();
        string? capturedPrompt = null;
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedPrompt = callInfo.Arg<LlmRequest>().Prompt;
                return new LlmResponse { Content = "ok" };
            });

        var contextSource = Substitute.For<IContextSource>();
        contextSource.Name.Returns("test-source");
        contextSource.GetContextAsync(Arg.Any<CancellationToken>()).Returns(new List<ContextDocument>
        {
            new() { Name = "doc1", Content = "context content", Source = "test" }
        });

        var step = new AgentLoopStep(provider, new ToolRegistry(), new AgentLoopOptions
        {
            ContextSources = new List<IContextSource> { contextSource }
        });

        await step.ExecuteAsync(new WorkflowContext());

        capturedPrompt.Should().Contain("context content");
    }

    [Fact]
    public async Task ExecuteAsync_SystemPromptIncluded()
    {
        var provider = Substitute.For<IAgentProvider>();
        string? capturedPrompt = null;
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedPrompt = callInfo.Arg<LlmRequest>().Prompt;
                return new LlmResponse { Content = "ok" };
            });

        var step = new AgentLoopStep(provider, new ToolRegistry(), new AgentLoopOptions
        {
            SystemPrompt = "You are a helpful assistant."
        });

        await step.ExecuteAsync(new WorkflowContext());

        capturedPrompt.Should().Contain("You are a helpful assistant.");
    }

    [Fact]
    public async Task ExecuteAsync_ToolDefsPassedToLlm()
    {
        var provider = Substitute.For<IAgentProvider>();
        IList<AgentTool>? capturedTools = null;
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedTools = callInfo.Arg<LlmRequest>().Tools;
                return new LlmResponse { Content = "ok" };
            });

        var registry = CreateRegistryWithTool("myTool", "r");
        var step = new AgentLoopStep(provider, registry, new AgentLoopOptions());

        await step.ExecuteAsync(new WorkflowContext());

        capturedTools.Should().NotBeNull();
        capturedTools!.Should().HaveCount(1);
        capturedTools[0].Name.Should().Be("myTool");
    }
}
