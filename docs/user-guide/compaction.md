# Context Compaction & Checkpointing

Long-running agent loops accumulate conversation history that can exceed LLM context windows. The compaction system automatically summarizes older messages while preserving recent context and system instructions. Checkpointing saves snapshots for recovery.

## IContextManager

Manages the conversation history with built-in compaction support:

```csharp
public interface IContextManager
{
    void AddMessage(ConversationMessage message);
    void AddToolCall(string toolName, string args, string result);
    IReadOnlyList<ConversationMessage> GetMessages();
    int EstimateTokenCount();
    Task<CompactionResult> CompactAsync(CompactionOptions options, CancellationToken ct = default);
    ContextSnapshot CreateSnapshot();
    void RestoreSnapshot(ContextSnapshot snapshot);
    void Clear();
}
```

The default implementation (`DefaultContextManager`) uses a simple chars/4 token estimator and supports both LLM-based and sliding-window compaction.

## ConversationMessage

```csharp
public sealed class ConversationMessage
{
    public ConversationRole Role { get; set; }   // System, User, Assistant, Tool
    public string Content { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public IDictionary<string, string> Metadata { get; set; }
    public bool IsCompacted { get; set; }         // True if this is a compaction summary
}
```

## CompactionOptions

| Property | Default | Description |
|----------|---------|-------------|
| `MaxTokens` | 100,000 | Token threshold that triggers compaction |
| `MaxMessages` | 200 | Message count threshold |
| `PreserveSystemMessages` | true | Keep system messages during compaction |
| `PreserveRecentCount` | 5 | Always keep the N most recent messages |
| `FocusInstructions` | null | Tell the summarizer what to focus on |
| `Strategy` | null | `ICompactionStrategy` to use (default: `SlidingWindowCompactionStrategy`) |

## Compaction Strategies

### SlidingWindowCompactionStrategy

No LLM needed. Keeps the first N and last M messages, drops the middle with a count marker:

```csharp
var strategy = new SlidingWindowCompactionStrategy(keepFirst: 2, keepLast: 5);
```

Output:
```
[Conversation summary]
[System]: You are a helpful assistant.
[User]: Help me analyze this dataset.
[... 47 messages omitted ...]
[Assistant]: The correlation coefficient is 0.87.
[User]: What about the outliers?
[Assistant]: I found 3 outliers...
```

### LlmCompactionStrategy

Uses an LLM to produce an intelligent summary of the compacted messages:

```csharp
var strategy = new LlmCompactionStrategy(llmProvider);
```

The strategy sends the messages with a summarization prompt, optionally including `FocusInstructions` to guide what information to preserve.

## CompactionResult

```csharp
public sealed class CompactionResult
{
    public int OriginalMessageCount { get; set; }
    public int CompactedMessageCount { get; set; }
    public int OriginalTokenEstimate { get; set; }
    public int CompactedTokenEstimate { get; set; }
    public string? Summary { get; set; }
}
```

## ICheckpointStore

Saves and loads context snapshots for recovery:

```csharp
public interface ICheckpointStore
{
    Task SaveAsync(string workflowId, string checkpointId, ContextSnapshot snapshot, CancellationToken ct = default);
    Task<ContextSnapshot?> LoadAsync(string workflowId, string checkpointId, CancellationToken ct = default);
    Task<IReadOnlyList<CheckpointInfo>> ListAsync(string workflowId, CancellationToken ct = default);
    Task DeleteAsync(string workflowId, string checkpointId, CancellationToken ct = default);
}
```

`InMemoryCheckpointStore` provides a `ConcurrentDictionary`-backed implementation for development and testing.

## Integration with AgentLoopStep

`AgentLoopStep` handles compaction and checkpointing automatically:

```csharp
var checkpointStore = new InMemoryCheckpointStore();

var workflow = new WorkflowBuilder()
    .AgentLoop(provider, registry, options =>
    {
        // Auto-compaction
        options.AutoCompact = true;
        options.MaxContextTokens = 50_000;
        options.CompactionStrategy = new LlmCompactionStrategy(provider);
        options.CompactionFocusInstructions = "Preserve all SQL queries and their results";

        // Checkpointing
        options.CheckpointStore = checkpointStore;
        options.CheckpointInterval = 3;  // Save every 3 iterations
    })
    .Build();

await workflow.RunAsync(context);

// List checkpoints after execution
var checkpoints = await checkpointStore.ListAsync(context.WorkflowId);
foreach (var cp in checkpoints)
{
    Console.WriteLine($"Checkpoint {cp.Id}: {cp.MessageCount} messages, ~{cp.EstimatedTokens} tokens");
}
```

### How Auto-Compaction Works

At the start of each iteration, `AgentLoopStep`:

1. Checks if `EstimateTokenCount()` exceeds `MaxContextTokens`
2. Fires `PreCompact` hook
3. Calls `CompactAsync()` which:
   - Separates system messages (preserved)
   - Keeps the `PreserveRecentCount` most recent messages
   - Summarizes everything else via the chosen strategy
   - Rebuilds the message list: system → summary → recent
4. Fires `PostCompact` hook

### Resuming from a Checkpoint

```csharp
var snapshot = await checkpointStore.LoadAsync(workflowId, "AgentLoop-iteration-6");
if (snapshot != null)
{
    var contextManager = new DefaultContextManager();
    contextManager.RestoreSnapshot(snapshot);

    var workflow = new WorkflowBuilder()
        .AgentLoop(provider, registry, options =>
        {
            options.ContextManager = contextManager;
            options.CheckpointStore = checkpointStore;
        })
        .Build();

    await workflow.RunAsync(context);
}
```

## Complete Example: Long-Running Research Agent

```csharp
var provider = new OllamaAgentProvider(new OllamaOptions
{
    BaseUrl = "http://localhost:11434",
    DefaultModel = "qwen3:30b-instruct"
});

var registry = new ToolRegistry();
registry.Register(new WebSearchToolProvider());
registry.Register(new FileSystemToolProvider());

var checkpointStore = new InMemoryCheckpointStore();

var workflow = new WorkflowBuilder()
    .AgentLoop(provider, registry, options =>
    {
        options.MaxIterations = 50;
        options.SystemPrompt = "You are a research assistant. Search the web, " +
            "read documents, and compile a comprehensive report.";

        // Compaction for long conversations
        options.AutoCompact = true;
        options.MaxContextTokens = 80_000;
        options.CompactionStrategy = new LlmCompactionStrategy(provider);
        options.CompactionFocusInstructions =
            "Preserve all research findings, source URLs, and key data points.";

        // Checkpoint every 5 iterations
        options.CheckpointStore = checkpointStore;
        options.CheckpointInterval = 5;

        options.StepName = "Researcher";
    })
    .Build();

await workflow.RunAsync(context);

var report = context.Properties["Researcher.Response"];
Console.WriteLine($"Completed in {context.Properties["Researcher.Iterations"]} iterations");
```
