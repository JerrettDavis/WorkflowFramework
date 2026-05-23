using FluentAssertions;
using NSubstitute;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.AI;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Agents.Tests.Agents;

[Feature("AgentLoopStep — characterization (Phase I coverage)")]
public class AgentLoopStepScenarios : TinyBddXunitBase
{
    public AgentLoopStepScenarios(ITestOutputHelper output) : base(output) { }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static AgentLoopStep MakeStep(AgentLoopOptions opts)
        => new(new EchoAgentProvider(), new ToolRegistry(), opts);

    // ── Name ────────────────────────────────────────────────────────────────

    [Scenario("Name defaults to 'AgentLoop' when StepName is null"), Fact]
    public async Task NameDefaultsToAgentLoop()
    {
        var sut = MakeStep(new AgentLoopOptions { MaxIterations = 1 });

        await Given("AgentLoopStep with no StepName", () => sut)
            .Then("Name is 'AgentLoop'", s =>
            {
                s.Name.Should().Be("AgentLoop");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Name uses StepName when provided"), Fact]
    public async Task NameUsesStepName()
    {
        var sut = MakeStep(new AgentLoopOptions { MaxIterations = 1, StepName = "MyAgent" });

        await Given("AgentLoopStep with StepName='MyAgent'", () => sut)
            .Then("Name is 'MyAgent'", s =>
            {
                s.Name.Should().Be("MyAgent");
                return true;
            })
            .AssertPassed();
    }

    // ── basic execution ──────────────────────────────────────────────────────

    [Scenario("ExecuteAsync stores response and iteration count on context"), Fact]
    public async Task ExecuteAsync_StoresResponseAndIterations()
    {
        var sut = MakeStep(new AgentLoopOptions { MaxIterations = 1 });
        var ctx = new WorkflowContext();
        await sut.ExecuteAsync(ctx);

        await Given("AgentLoopStep run with EchoProvider", () => ctx)
            .Then("Response and Iterations are stored on context", c =>
            {
                c.Properties.Should().ContainKey("AgentLoop.Response");
                c.Properties.Should().ContainKey("AgentLoop.Iterations");
                ((int)c.Properties["AgentLoop.Iterations"]!).Should().Be(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ExecuteAsync with SystemPrompt includes system prompt in context"), Fact]
    public async Task ExecuteAsync_WithSystemPrompt()
    {
        var sut = MakeStep(new AgentLoopOptions
        {
            MaxIterations = 1,
            SystemPrompt = "You are a helpful assistant."
        });
        var ctx = new WorkflowContext();
        await sut.ExecuteAsync(ctx);

        await Given("AgentLoopStep with SystemPrompt", () => ctx)
            .Then("Response contains echoed system prompt", c =>
            {
                var response = (string)c.Properties["AgentLoop.Response"]!;
                response.Should().Contain("You are a helpful assistant.");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ExecuteAsync with InitialUserMessageTemplate renders and adds user message"), Fact]
    public async Task ExecuteAsync_WithInitialUserMessageTemplate()
    {
        var sut = MakeStep(new AgentLoopOptions
        {
            MaxIterations = 1,
            InitialUserMessageTemplate = "Process item: {{itemId}}"
        });
        var ctx = new WorkflowContext();
        ctx.Properties["itemId"] = "ABC-123";
        await sut.ExecuteAsync(ctx);

        await Given("AgentLoopStep with InitialUserMessageTemplate containing {{itemId}}", () => ctx)
            .Then("response contains rendered user message", c =>
            {
                var response = (string)c.Properties["AgentLoop.Response"]!;
                response.Should().Contain("ABC-123");
                return true;
            })
            .AssertPassed();
    }

    // ── auto-compaction ──────────────────────────────────────────────────────

    [Scenario("AutoCompact fires compaction when token count exceeds threshold"), Fact]
    public async Task AutoCompact_FiresWhenOverThreshold()
    {
        var contextManager = Substitute.For<IContextManager>();
        // First call: over threshold; subsequent calls: under
        contextManager.EstimateTokenCount().Returns(200, 50);
        contextManager.GetMessages().Returns(new List<ConversationMessage>());

        var compacted = false;
        contextManager.CompactAsync(Arg.Any<CompactionOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ => { compacted = true; return Task.FromResult(new CompactionResult()); });

        var sut = new AgentLoopStep(
            new EchoAgentProvider(),
            new ToolRegistry(),
            new AgentLoopOptions
            {
                MaxIterations = 1,
                AutoCompact = true,
                MaxContextTokens = 100,
                ContextManager = contextManager
            });

        var ctx = new WorkflowContext();
        await sut.ExecuteAsync(ctx);

        await Given("context manager reporting 200 tokens (threshold 100)", () => compacted)
            .Then("CompactAsync was called", c =>
            {
                c.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("AutoCompact=false skips compaction even when over threshold"), Fact]
    public async Task AutoCompact_SkippedWhenDisabled()
    {
        var contextManager = Substitute.For<IContextManager>();
        contextManager.EstimateTokenCount().Returns(999999);
        contextManager.GetMessages().Returns(new List<ConversationMessage>());

        var sut = new AgentLoopStep(
            new EchoAgentProvider(),
            new ToolRegistry(),
            new AgentLoopOptions
            {
                MaxIterations = 1,
                AutoCompact = false,
                MaxContextTokens = 100,
                ContextManager = contextManager
            });

        var ctx = new WorkflowContext();
        await sut.ExecuteAsync(ctx);

        await Given("AutoCompact=false with high token count", () => contextManager)
            .Then("CompactAsync is never called", m =>
            {
                m.DidNotReceive().CompactAsync(Arg.Any<CompactionOptions>(), Arg.Any<CancellationToken>());
                return true;
            })
            .AssertPassed();
    }

    // ── tool calls ───────────────────────────────────────────────────────────

    [Scenario("Tool call is invoked and result stored in context"), Fact]
    public async Task ToolCall_IsInvokedAndResultStored()
    {
        var provider = Substitute.For<IAgentProvider>();
        provider.Name.Returns("test");

        // First call returns a tool call; second (after result) returns no tool calls
        var callCount = 0;
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return Task.FromResult(new LlmResponse
                    {
                        Content = "calling tool",
                        FinishReason = "tool_calls",
                        ToolCalls = [new ToolCall { ToolName = "greet", Arguments = "{\"name\":\"World\"}" }]
                    });
                }
                return Task.FromResult(new LlmResponse
                {
                    Content = "done",
                    FinishReason = "stop",
                    ToolCalls = []
                });
            });

        var registry = new ToolRegistry();
        var toolProvider = Substitute.For<IToolProvider>();
        toolProvider.ListToolsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ToolDefinition>>(
                [new ToolDefinition { Name = "greet", Description = "Greets" }]));
        toolProvider.InvokeToolAsync("greet", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ToolResult { Content = "Hello, World!" }));
        registry.Register(toolProvider);

        var sut = new AgentLoopStep(provider, registry, new AgentLoopOptions { MaxIterations = 5 });
        var ctx = new WorkflowContext();
        await sut.ExecuteAsync(ctx);

        await Given("AgentLoopStep with a tool-calling provider", () => ctx)
            .Then("ToolResults are stored on context and contain one result", c =>
            {
                var toolResults = (List<ToolResult>)c.Properties["AgentLoop.ToolResults"]!;
                toolResults.Should().HaveCount(1);
                toolResults[0].Content.Should().Be("Hello, World!");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Hook Deny decision blocks tool invocation"), Fact]
    public async Task HookDeny_BlocksToolInvocation()
    {
        var provider = Substitute.For<IAgentProvider>();
        provider.Name.Returns("test");

        var callCount = 0;
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                    return Task.FromResult(new LlmResponse
                    {
                        Content = "calling tool",
                        FinishReason = "tool_calls",
                        ToolCalls = [new ToolCall { ToolName = "danger", Arguments = "{}" }]
                    });
                return Task.FromResult(new LlmResponse { Content = "done", ToolCalls = [] });
            });

        var toolInvoked = false;
        var registry = new ToolRegistry();
        var toolProvider = Substitute.For<IToolProvider>();
        toolProvider.ListToolsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ToolDefinition>>(
                [new ToolDefinition { Name = "danger", Description = "Dangerous" }]));
        toolProvider.InvokeToolAsync("danger", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => { toolInvoked = true; return Task.FromResult(new ToolResult { Content = "bad" }); });
        registry.Register(toolProvider);

        // Hook that denies all tool calls
        var hook = Substitute.For<IAgentHook>();
        hook.Matcher.Returns((string?)null); // match everything
        hook.ExecuteAsync(Arg.Any<AgentHookEvent>(), Arg.Any<HookContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(HookResult.DenyResult("not allowed")));

        var hooks = new HookPipeline();
        hooks.Add(hook);

        var sut = new AgentLoopStep(provider, registry, new AgentLoopOptions
        {
            MaxIterations = 5,
            Hooks = hooks
        });
        var ctx = new WorkflowContext();
        await sut.ExecuteAsync(ctx);

        await Given("hook that denies 'danger' tool call", () => toolInvoked)
            .Then("tool was never invoked", invoked =>
            {
                invoked.Should().BeFalse();
                return true;
            })
            .AssertPassed();
    }

    // ── checkpointing ────────────────────────────────────────────────────────

    [Scenario("Checkpoint is saved after tool call iteration"), Fact]
    public async Task Checkpoint_SavedAfterToolCallIteration()
    {
        var saveCount = 0;
        var checkpoint = Substitute.For<ICheckpointStore>();
        checkpoint.SaveAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ContextSnapshot>(), Arg.Any<CancellationToken>())
            .Returns(_ => { saveCount++; return Task.CompletedTask; });

        // Provider that returns a tool call on iteration 1, and no tool calls on iteration 2
        var provider = Substitute.For<IAgentProvider>();
        provider.Name.Returns("test");
        var callCount = 0;
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                    return Task.FromResult(new LlmResponse
                    {
                        Content = "calling",
                        ToolCalls = [new ToolCall { ToolName = "noop", Arguments = "{}" }]
                    });
                return Task.FromResult(new LlmResponse { Content = "done", ToolCalls = [] });
            });

        // Tool provider that handles the noop call
        var toolProvider = Substitute.For<IToolProvider>();
        toolProvider.ListToolsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ToolDefinition>>(
                [new ToolDefinition { Name = "noop", Description = "No-op" }]));
        toolProvider.InvokeToolAsync("noop", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ToolResult { Content = "ok" }));
        var registry = new ToolRegistry();
        registry.Register(toolProvider);

        var sut = new AgentLoopStep(
            provider,
            registry,
            new AgentLoopOptions
            {
                MaxIterations = 5,
                CheckpointStore = checkpoint,
                CheckpointInterval = 1,
                ContextManager = new DefaultContextManager()
            });

        var ctx = new WorkflowContext();
        await sut.ExecuteAsync(ctx);

        await Given("AgentLoopStep that makes one tool call then stops", () => saveCount)
            .Then("SaveAsync was called at least once (after the tool-call iteration)", count =>
            {
                count.Should().BeGreaterThan(0);
                return true;
            })
            .AssertPassed();
    }

    // ── constructor validation ───────────────────────────────────────────────

    [Scenario("Null provider throws ArgumentNullException"), Fact]
    public async Task NullProviderThrows()
    {
        Exception? caught = null;
        try { _ = new AgentLoopStep(null!, new ToolRegistry(), new AgentLoopOptions()); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null provider", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null registry throws ArgumentNullException"), Fact]
    public async Task NullRegistryThrows()
    {
        Exception? caught = null;
        try { _ = new AgentLoopStep(new EchoAgentProvider(), null!, new AgentLoopOptions()); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null registry", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null options throws ArgumentNullException"), Fact]
    public async Task NullOptionsThrows()
    {
        Exception? caught = null;
        try { _ = new AgentLoopStep(new EchoAgentProvider(), new ToolRegistry(), null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null options", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    // ── AgentLoopOptions defaults ────────────────────────────────────────────

    [Scenario("AgentLoopOptions defaults are correct"), Fact]
    public async Task AgentLoopOptions_Defaults()
    {
        var opts = new AgentLoopOptions();

        await Given("default AgentLoopOptions", () => opts)
            .Then("MaxIterations=10, AutoCompact=true, MaxContextTokens=100000, CheckpointInterval=1", o =>
            {
                o.MaxIterations.Should().Be(10);
                o.AutoCompact.Should().BeTrue();
                o.MaxContextTokens.Should().Be(100000);
                o.CheckpointInterval.Should().Be(1);
                o.CompactionFocusInstructions.Should().BeNull();
                o.CompactionStrategy.Should().BeNull();
                o.CheckpointStore.Should().BeNull();
                o.InitialUserMessageTemplate.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }
}
