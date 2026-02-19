# Hook Lifecycle System

The hook system lets you intercept, inspect, and control agent execution at every lifecycle point. Hooks can allow, deny, or modify operations — enabling audit logging, safety guardrails, approval gates, and dynamic argument rewriting.

## AgentHookEvent

All 13 lifecycle events:

| Event | When It Fires |
|-------|---------------|
| `WorkflowStarting` | Before the workflow begins |
| `PreAgentPrompt` | Before sending a prompt to the LLM |
| `PreToolCall` | Before invoking a tool |
| `PostToolCall` | After a successful tool invocation |
| `PostToolCallFailure` | After a tool invocation throws an exception |
| `StepCompleted` | After a workflow step finishes |
| `SubWorkflowStarting` | Before a sub-workflow begins |
| `SubWorkflowCompleted` | After a sub-workflow finishes |
| `PreCompact` | Before context compaction |
| `PostCompact` | After context compaction |
| `Checkpoint` | When a checkpoint is saved |
| `Notification` | When an agent emits a notification |
| `WorkflowCompleted` | After the workflow finishes |

## IAgentHook

```csharp
public interface IAgentHook
{
    /// Regex pattern to match against "Event:StepName:ToolName". Null matches all.
    string? Matcher { get; }

    Task<HookResult> ExecuteAsync(AgentHookEvent hookEvent, HookContext context, CancellationToken ct = default);
}
```

The `Matcher` is a regex tested against a string built from `"{Event}:{StepName}:{ToolName}"`. If null, the hook fires for every event.

## HookResult & HookDecision

```csharp
public enum HookDecision
{
    Allow,   // Let the operation proceed
    Deny,    // Block the operation
    Modify   // Allow but change arguments
}

public sealed class HookResult
{
    public HookDecision Decision { get; set; }
    public string? Reason { get; set; }
    public string? ModifiedArgs { get; set; }    // For Modify decisions
    public string? OutputMessage { get; set; }

    // Factory methods
    public static HookResult AllowResult(string? reason = null);
    public static HookResult DenyResult(string? reason = null);
    public static HookResult ModifyResult(string modifiedArgs, string? reason = null);
}
```

## HookPipeline

`HookPipeline` runs hooks in registration order. A `Deny` result stops execution immediately. For `Modify`, the last modifier wins:

```csharp
var pipeline = new HookPipeline();
pipeline.Add(new AuditLogHook());          // Runs first
pipeline.Add(new SafetyGuardrailHook());   // Can deny
pipeline.Add(new ArgRewriteHook());        // Can modify

// Use with AgentLoopStep
var workflow = new WorkflowBuilder()
    .AgentLoop(provider, registry, options =>
    {
        options.Hooks = pipeline;
    })
    .Build();
```

## Handler Types

### CodeHook — Inline Delegates

The simplest hook type. Wrap any `Func<HookContext, Task<HookResult>>`:

```csharp
var auditHook = new CodeHook(
    handler: async ctx =>
    {
        Console.WriteLine($"[AUDIT] {ctx.Event}: tool={ctx.ToolName}, args={ctx.ToolArgs}");
        return HookResult.AllowResult();
    },
    matcher: "PreToolCall"  // Only fire for PreToolCall events
);

var denyDangerousHook = new CodeHook(
    handler: async ctx =>
    {
        if (ctx.ToolName == "delete_database")
            return HookResult.DenyResult("Dangerous tool blocked");
        return HookResult.AllowResult();
    },
    matcher: "PreToolCall:.*:delete_.*"  // Match tools starting with "delete_"
);
```

### CommandHook — Shell Commands

Runs an external process. Sends `HookContext` as JSON on stdin, reads `HookResult` JSON from stdout:

```csharp
var hook = new CommandHook(
    command: "python",
    args: new[] { "validate_tool_call.py" },
    matcher: "PreToolCall",
    timeout: TimeSpan.FromSeconds(10)
);
```

**stdin** (JSON):
```json
{
    "event": "PreToolCall",
    "stepName": "Agent",
    "toolName": "run_query",
    "toolArgs": "{\"sql\": \"SELECT * FROM users\"}"
}
```

**stdout** (JSON):
```json
{
    "Decision": 0,
    "Reason": "Query approved",
    "ModifiedArgs": null
}
```

If the process exits with a non-zero code, the hook returns `Deny`.

### PromptHook — LLM-Evaluated

Uses an `IAgentProvider` to evaluate whether to allow or deny. The LLM response is scanned for keywords like "deny", "block", or "reject":

```csharp
var hook = new PromptHook(
    provider: llmProvider,
    promptTemplate: """
        Should this tool call be allowed?
        Event: {event}
        Tool: {toolName}
        Arguments: {toolArgs}
        
        Respond with ALLOW or DENY and a brief reason.
        """,
    matcher: "PreToolCall"
);
```

Template variables: `{event}`, `{toolName}`, `{toolArgs}`.

## HookContext

The context object passed to every hook:

```csharp
public sealed class HookContext
{
    public AgentHookEvent Event { get; set; }
    public string? StepName { get; set; }
    public string? ToolName { get; set; }
    public string? ToolArgs { get; set; }
    public ToolResult? ToolResult { get; set; }
    public IWorkflowContext? WorkflowContext { get; set; }
    public IDictionary<string, object?> Metadata { get; set; }
    public IList<ConversationMessage> Messages { get; set; }
}
```

## Comparison with Claude Code Hooks

| Feature | WorkflowFramework Hooks | Claude Code Hooks |
|---------|------------------------|-------------------|
| Events | 13 lifecycle events | Tool calls, notifications |
| Matching | Regex on `Event:Step:Tool` | Glob patterns |
| Decisions | Allow / Deny / Modify | Allow / Deny |
| Arg rewriting | Yes (`ModifiedArgs`) | No |
| Handler types | Code, Command, Prompt (LLM) | Command (shell only) |
| Pipeline ordering | Explicit registration order | File-based ordering |
| LLM evaluation | Built-in (`PromptHook`) | Not available |
| Context access | Full workflow context | Limited to tool call info |

## Complete Example

```csharp
var pipeline = new HookPipeline();

// 1. Audit all events
pipeline.Add(new CodeHook(async ctx =>
{
    logger.LogInformation("Hook: {Event} tool={Tool}", ctx.Event, ctx.ToolName);
    return HookResult.AllowResult();
}));

// 2. Block file deletion tools
pipeline.Add(new CodeHook(
    async ctx => ctx.ToolName?.Contains("delete") == true
        ? HookResult.DenyResult("Deletion not allowed")
        : HookResult.AllowResult(),
    matcher: "PreToolCall"));

// 3. External validation via shell script
pipeline.Add(new CommandHook("./validate.sh", matcher: "PreToolCall"));

// 4. LLM safety check for sensitive tools
pipeline.Add(new PromptHook(provider,
    "Is this tool call safe? Tool: {toolName}, Args: {toolArgs}. Reply ALLOW or DENY.",
    matcher: "PreToolCall:.*:run_query"));

var workflow = new WorkflowBuilder()
    .AgentLoop(provider, registry, opts => opts.Hooks = pipeline)
    .Build();
```
