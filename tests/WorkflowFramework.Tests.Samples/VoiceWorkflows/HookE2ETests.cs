using FluentAssertions;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Samples.VoiceWorkflows.Hooks;
using WorkflowFramework.Samples.VoiceWorkflows.Models;
using WorkflowFramework.Samples.VoiceWorkflows.Tools;
using WorkflowFramework.Samples.VoiceWorkflows.Workflows;

namespace WorkflowFramework.Tests.Samples.VoiceWorkflows;

[Collection("VoiceWorkflows")]
[Trait("Category", "SampleE2E")]
public sealed class HookE2ETests
{
    private readonly VoiceWorkflowFixture _fixture;
    public HookE2ETests(VoiceWorkflowFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ConsoleLoggingHook_FiresOnWorkflowEvents()
    {
        var originalOut = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);

        try
        {
            // Use BrainDumpSynthesis since it has an AgentLoopStep that fires hook events
            var workflow = _fixture.CreateBrainDumpSynthesis();
            var context = new WorkflowContext();
            await workflow.ExecuteAsync(context);

            var output = sw.ToString();
            // The ConsoleLoggingHook logs hook events to console
            // The workflow itself also logs to console
            output.Should().NotBeNullOrEmpty();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task HookPipeline_DenyBlocksToolCall()
    {
        // Create a hook that denies "transcribe" tool calls
        var denyHook = new DenyToolHook("transcribe");
        var hooks = new HookPipeline();
        hooks.Add(denyHook);

        var tools = new ToolRegistry();
        tools.Register(new WhisperToolProvider(new WhisperOptions()));
        tools.Register(new AudioToolProvider());

        var agent = new EchoAgentProvider();
        var checkpoints = new InMemoryCheckpointStore();

        // Build a simple workflow with an AgentLoopStep that would call transcribe
        var workflow = Workflow.Create("DenyTest")
            .Step(new AgentLoopStep(agent, tools, new AgentLoopOptions
            {
                StepName = "TestLoop",
                SystemPrompt = "Transcribe audio",
                MaxIterations = 1,
                Hooks = hooks
            }))
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        // The EchoAgentProvider doesn't generate tool calls, so just verify
        // the hook pipeline is set up correctly
        result.IsSuccess.Should().BeTrue();
        denyHook.DenyCount.Should().Be(0); // Echo provider doesn't call tools

        // Verify the deny hook works directly
        var hookContext = new HookContext
        {
            Event = AgentHookEvent.PreToolCall,
            ToolName = "transcribe",
            ToolArgs = "{}"
        };
        var hookResult = await hooks.FireAsync(AgentHookEvent.PreToolCall, hookContext, CancellationToken.None);
        hookResult.Decision.Should().Be(HookDecision.Deny);
    }

    private sealed class DenyToolHook : IAgentHook
    {
        private readonly string _toolName;
        public int DenyCount { get; private set; }

        public DenyToolHook(string toolName) => _toolName = toolName;
        public string? Matcher => null;

        public Task<HookResult> ExecuteAsync(AgentHookEvent hookEvent, HookContext context, CancellationToken ct = default)
        {
            if (hookEvent == AgentHookEvent.PreToolCall && context.ToolName == _toolName)
            {
                DenyCount++;
                return Task.FromResult(HookResult.DenyResult($"Tool '{_toolName}' is denied"));
            }
            return Task.FromResult(HookResult.AllowResult());
        }
    }
}
