using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Extensions.HumanTasks;
using WorkflowFramework;
using WorkflowFramework.Samples.VoiceWorkflows.Extensions;
using WorkflowFramework.Samples.VoiceWorkflows.Workflows;

// ═══════════════════════════════════════════════════════════════
//  VoiceWorkflows — Agentic Voice/Transcription Pipelines
// ═══════════════════════════════════════════════════════════════

Console.WriteLine("╔══════════════════════════════════════════════════════╗");
Console.WriteLine("║  🎙️ VoiceWorkflows — Voice Processing Pipelines     ║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");
Console.WriteLine();

var workflows = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["quick-transcript"] = "QuickTranscript — Record → Transcribe → Cleanup → Review",
    ["meeting-notes"] = "MeetingNotes — Transcribe → Speakers → Notes → Action Items → Review",
    ["blog-interview"] = "BlogInterview — Multi-phase agentic interview → Blog post",
    ["brain-dump"] = "BrainDumpSynthesis — Record → Transcribe → Agent synthesize → Review",
    ["podcast"] = "PodcastTranscript — Transcribe → Parallel(Summary + Format) → Merge → Review"
};

// Parse args
if (args.Contains("--list"))
{
    Console.WriteLine("Available workflows:");
    Console.WriteLine();
    foreach (var (key, desc) in workflows)
        Console.WriteLine($"  {key,-20} {desc}");
    Console.WriteLine();
    Console.WriteLine("Usage: --workflow <name> [--use-ollama]");
    return;
}

var workflowName = "quick-transcript";
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--workflow" && i + 1 < args.Length)
        workflowName = args[++i];
}

if (!workflows.ContainsKey(workflowName))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Unknown workflow: {workflowName}");
    Console.ResetColor();
    Console.WriteLine("Use --list to see available workflows.");
    return;
}

Console.WriteLine($"▶ Running: {workflows[workflowName]}");
Console.WriteLine();

// Set up DI
var services = new ServiceCollection();
services.AddVoiceWorkflows(args);
using var sp = services.BuildServiceProvider();

var agent = sp.GetRequiredService<IAgentProvider>();
var tools = sp.GetRequiredService<ToolRegistry>();
var inbox = sp.GetRequiredService<ITaskInbox>();
var hooks = sp.GetRequiredService<HookPipeline>();
var checkpoints = sp.GetRequiredService<ICheckpointStore>();

// Build the selected workflow
var workflow = workflowName.ToLowerInvariant() switch
{
    "quick-transcript" => VoiceWorkflowPresets.QuickTranscript(agent, tools, inbox, hooks, checkpoints),
    "meeting-notes" => VoiceWorkflowPresets.MeetingNotes(agent, tools, inbox, hooks, checkpoints),
    "blog-interview" => VoiceWorkflowPresets.BlogInterview(agent, tools, inbox, hooks, checkpoints),
    "brain-dump" => VoiceWorkflowPresets.BrainDumpSynthesis(agent, tools, inbox, hooks, checkpoints),
    "podcast" => VoiceWorkflowPresets.PodcastTranscript(agent, tools, inbox, hooks, checkpoints),
    _ => throw new InvalidOperationException($"Unknown workflow: {workflowName}")
};

// Execute
var context = new WorkflowContext();
var sw = System.Diagnostics.Stopwatch.StartNew();
var result = await workflow.ExecuteAsync(context);
sw.Stop();

Console.WriteLine();
Console.WriteLine($"▶ Workflow completed in {sw.ElapsedMilliseconds}ms — Status: {result.Status}");
if (result.Status == WorkflowStatus.Faulted && context.Errors.Count > 0)
{
    Console.ForegroundColor = ConsoleColor.Red;
    foreach (var err in context.Errors)
        Console.WriteLine($"  ❌ [{err.StepName}] {err.Exception.Message}");
    Console.ResetColor();
}
Console.WriteLine();

// Print final output if available
if (context.Properties.TryGetValue("finalOutput", out var output) && output is string finalStr)
{
    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine("📄 Final Output:");
    Console.WriteLine("───────────────────────────────────────────────────────");
    var preview = finalStr.Length > 500 ? finalStr[..500] + "\n..." : finalStr;
    Console.WriteLine(preview);
}
else if (context.Properties.TryGetValue("processedText", out var processed) && processed is string processedStr)
{
    Console.WriteLine("═══════════════════════════════════════════════════════");
    Console.WriteLine("📄 Processed Text:");
    Console.WriteLine("───────────────────────────────────────────────────────");
    var preview = processedStr.Length > 500 ? processedStr[..500] + "\n..." : processedStr;
    Console.WriteLine(preview);
}

// Show checkpoint info
var checkpointList = await checkpoints.ListAsync(context.WorkflowId);
if (checkpointList.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine($"💾 Checkpoints saved: {checkpointList.Count}");
    foreach (var cp in checkpointList)
        Console.WriteLine($"   • {cp.Id} ({cp.StepName}, {cp.MessageCount} messages, ~{cp.EstimatedTokens} tokens)");
}

Console.WriteLine();
Console.WriteLine("═══════════════════════════════════════════════════════");
Console.WriteLine("✨ Done!");
