using Xunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Extensions.HumanTasks;
using WorkflowFramework.Samples.VoiceWorkflows.Extensions;
using WorkflowFramework.Samples.VoiceWorkflows.Hooks;
using WorkflowFramework.Samples.VoiceWorkflows.Workflows;

namespace WorkflowFramework.Tests.E2E;

/// <summary>
/// E2E tests for the VoiceWorkflows sample app using simulated/mock providers (no Ollama required).
/// Each workflow preset is tested to completion with the EchoAgentProvider and simulated tools.
/// </summary>
[Trait("Category", "E2E")]
public class VoiceWorkflowsE2ETests
{
    private static (IAgentProvider agent, ToolRegistry tools, ITaskInbox inbox, HookPipeline hooks, ICheckpointStore checkpoints) BuildDependencies()
    {
        var services = new ServiceCollection();
        services.AddVoiceWorkflows(); // no --use-ollama → EchoAgentProvider
        var sp = services.BuildServiceProvider();

        return (
            sp.GetRequiredService<IAgentProvider>(),
            sp.GetRequiredService<ToolRegistry>(),
            sp.GetRequiredService<ITaskInbox>(),
            sp.GetRequiredService<HookPipeline>(),
            sp.GetRequiredService<ICheckpointStore>()
        );
    }

    [Fact(Timeout = 30_000)]
    public async Task QuickTranscript_CompletesSuccessfully()
    {
        var (agent, tools, inbox, hooks, checkpoints) = BuildDependencies();
        var workflow = VoiceWorkflowPresets.QuickTranscript(agent, tools, inbox, hooks, checkpoints);

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.Status.Should().Be(WorkflowStatus.Completed);
        context.Properties.Should().ContainKey("processedText");
    }

    [Fact(Timeout = 30_000)]
    public async Task MeetingNotes_CompletesSuccessfully()
    {
        var (agent, tools, inbox, hooks, checkpoints) = BuildDependencies();
        var workflow = VoiceWorkflowPresets.MeetingNotes(agent, tools, inbox, hooks, checkpoints);

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.Status.Should().Be(WorkflowStatus.Completed);
        context.Properties.Should().ContainKey("meetingNotes");
        context.Properties.Should().ContainKey("actionItems");
    }

    [Fact(Timeout = 60_000)]
    public async Task BlogInterview_CompletesSuccessfully()
    {
        var (agent, tools, inbox, hooks, checkpoints) = BuildDependencies();
        var workflow = VoiceWorkflowPresets.BlogInterview(agent, tools, inbox, hooks, checkpoints);

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.Status.Should().Be(WorkflowStatus.Completed);
        context.Properties.Should().ContainKey("finalOutput");
    }

    [Fact(Timeout = 30_000)]
    public async Task BrainDumpSynthesis_CompletesSuccessfully()
    {
        var (agent, tools, inbox, hooks, checkpoints) = BuildDependencies();
        var workflow = VoiceWorkflowPresets.BrainDumpSynthesis(agent, tools, inbox, hooks, checkpoints);

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.Status.Should().Be(WorkflowStatus.Completed);
        context.Properties.Should().ContainKey("finalOutput");
    }

    [Fact(Timeout = 30_000)]
    public async Task PodcastTranscript_CompletesSuccessfully()
    {
        var (agent, tools, inbox, hooks, checkpoints) = BuildDependencies();
        var workflow = VoiceWorkflowPresets.PodcastTranscript(agent, tools, inbox, hooks, checkpoints);

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.Status.Should().Be(WorkflowStatus.Completed);
        context.Properties.Should().ContainKey("finalOutput");
    }
}
