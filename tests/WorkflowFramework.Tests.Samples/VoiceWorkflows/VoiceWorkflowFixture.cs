using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Extensions.HumanTasks;
using WorkflowFramework.Samples.VoiceWorkflows;
using WorkflowFramework.Samples.VoiceWorkflows.Hooks;
using WorkflowFramework.Samples.VoiceWorkflows.Models;
using WorkflowFramework.Samples.VoiceWorkflows.Tools;
using WorkflowFramework.Samples.VoiceWorkflows.Workflows;

namespace WorkflowFramework.Tests.Samples.VoiceWorkflows;

public sealed class VoiceWorkflowFixture
{
    public IAgentProvider Agent { get; } = new EchoAgentProvider();
    public ToolRegistry Tools { get; }
    public ITaskInbox Inbox { get; } = new SimulatedHumanTaskInbox();
    public HookPipeline Hooks { get; }
    public ICheckpointStore Checkpoints { get; } = new InMemoryCheckpointStore();

    public VoiceWorkflowFixture()
    {
        Tools = new ToolRegistry();
        Tools.Register(new WhisperToolProvider(new WhisperOptions()));
        Tools.Register(new SpeakerDiarizationToolProvider());
        Tools.Register(new AudioToolProvider());
        Tools.Register(new TextToolProvider());

        Hooks = new HookPipeline();
        Hooks.Add(new ConsoleLoggingHook());
    }

    public IWorkflow CreateQuickTranscript() =>
        VoiceWorkflowPresets.QuickTranscript(Agent, Tools, Inbox, Hooks, Checkpoints);

    public IWorkflow CreateMeetingNotes() =>
        VoiceWorkflowPresets.MeetingNotes(Agent, Tools, Inbox, Hooks, Checkpoints);

    public IWorkflow CreateBlogInterview() =>
        VoiceWorkflowPresets.BlogInterview(Agent, Tools, Inbox, Hooks, Checkpoints);

    public IWorkflow CreateBrainDumpSynthesis() =>
        VoiceWorkflowPresets.BrainDumpSynthesis(Agent, Tools, Inbox, Hooks, Checkpoints);

    public IWorkflow CreatePodcastTranscript() =>
        VoiceWorkflowPresets.PodcastTranscript(Agent, Tools, Inbox, Hooks, Checkpoints);
}

[CollectionDefinition("VoiceWorkflows")]
public class VoiceWorkflowCollection : ICollectionFixture<VoiceWorkflowFixture> { }
