using WorkflowFramework.Extensions.Agents;

namespace WorkflowFramework.Samples.VoiceWorkflows.Hooks;

/// <summary>Agent hook that logs all events to console with colors.</summary>
public sealed class ConsoleLoggingHook : IAgentHook
{
    public string? Matcher => null; // match all events

    public Task<HookResult> ExecuteAsync(AgentHookEvent hookEvent, HookContext context, CancellationToken ct = default)
    {
        var (color, icon) = hookEvent switch
        {
            AgentHookEvent.PreToolCall => (ConsoleColor.Cyan, "🔧"),
            AgentHookEvent.PostToolCall => (ConsoleColor.Green, "✅"),
            AgentHookEvent.PostToolCallFailure => (ConsoleColor.Red, "❌"),
            AgentHookEvent.PreCompact => (ConsoleColor.Magenta, "📦"),
            AgentHookEvent.PostCompact => (ConsoleColor.Magenta, "📦"),
            AgentHookEvent.Checkpoint => (ConsoleColor.Blue, "💾"),
            AgentHookEvent.PreAgentPrompt => (ConsoleColor.DarkYellow, "🤖"),
            AgentHookEvent.WorkflowStarting => (ConsoleColor.White, "▶️"),
            AgentHookEvent.WorkflowCompleted => (ConsoleColor.White, "⏹️"),
            AgentHookEvent.StepCompleted => (ConsoleColor.DarkGreen, "✔️"),
            _ => (ConsoleColor.Gray, "ℹ️")
        };

        Console.ForegroundColor = color;

        var detail = hookEvent switch
        {
            AgentHookEvent.PreToolCall => $"Calling tool: {context.ToolName}",
            AgentHookEvent.PostToolCall => $"Tool {context.ToolName} completed ({context.ToolResult?.Content?.Length ?? 0} chars)",
            AgentHookEvent.PostToolCallFailure => $"Tool {context.ToolName} FAILED: {context.ToolResult?.Content}",
            AgentHookEvent.PreCompact => "Context compaction starting...",
            AgentHookEvent.PostCompact => "Context compacted",
            _ => $"[{context.StepName}]"
        };

        Console.WriteLine($"  {icon} [{hookEvent}] {detail}");
        Console.ResetColor();

        return Task.FromResult(HookResult.AllowResult());
    }
}
