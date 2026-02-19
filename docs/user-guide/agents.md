# Agentic Workflows

The `WorkflowFramework.Extensions.Agents` package extends the AI integration with autonomous agent loops, tool registries, and context management. Where [AI Agents](ai-agents.md) covers single LLM calls and decisions, this package enables multi-turn agentic workflows with a plan→act→observe loop.

## Installation

```bash
dotnet add package WorkflowFramework.Extensions.Agents
```

## Key Types

| Type | Purpose |
|------|---------|
| `IToolProvider` | Discovers and invokes tools |
| `ToolDefinition` | Describes a tool (name, description, JSON schema) |
| `ToolResult` | Result of a tool invocation |
| `ToolRegistry` | Aggregates multiple `IToolProvider` instances |
| `AgentLoopStep` | Autonomous plan→act→observe loop |
| `ToolCallStep` | Single tool invocation step |
| `IContextSource` / `ContextAggregator` | Inject context into agent prompts |
| `HookPipeline` | Lifecycle hooks for interception |

## IToolProvider

The core abstraction for tool discovery and invocation:

```csharp
public interface IToolProvider
{
    Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken ct = default);
    Task<ToolResult> InvokeToolAsync(string toolName, string argumentsJson, CancellationToken ct = default);
}
```

Each tool is described by a `ToolDefinition`:

```csharp
public sealed class ToolDefinition
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string? ParametersSchema { get; set; }  // JSON Schema
    public IDictionary<string, string> Metadata { get; set; }
}
```

Tool invocations return a `ToolResult`:

```csharp
public sealed class ToolResult
{
    public string Content { get; set; }
    public bool IsError { get; set; }
    public IDictionary<string, string> Metadata { get; set; }
}
```

### Implementing a Tool Provider

```csharp
public class WeatherToolProvider : IToolProvider
{
    public Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken ct = default)
    {
        var tools = new List<ToolDefinition>
        {
            new ToolDefinition
            {
                Name = "get_weather",
                Description = "Gets current weather for a city",
                ParametersSchema = """{"type":"object","properties":{"city":{"type":"string"}}}"""
            }
        };
        return Task.FromResult<IReadOnlyList<ToolDefinition>>(tools);
    }

    public async Task<ToolResult> InvokeToolAsync(string toolName, string argumentsJson, CancellationToken ct)
    {
        // Parse args, call weather API, return result
        return new ToolResult { Content = """{"temp": 72, "condition": "sunny"}""" };
    }
}
```

## ToolRegistry

`ToolRegistry` aggregates multiple providers into a single tool namespace. When tool names conflict, the last-registered provider wins:

```csharp
var registry = new ToolRegistry();
registry.Register(new WeatherToolProvider());
registry.Register(new DatabaseToolProvider());

// List all tools across providers
var allTools = await registry.ListAllToolsAsync();

// Invoke by name (searches last-registered first)
var result = await registry.InvokeAsync("get_weather", """{"city":"Austin"}""");
```

## AgentLoopStep

The core agentic step. Runs an autonomous plan→act→observe loop:

1. **Plan** — Sends conversation history + tool definitions to the LLM
2. **Act** — Executes any tool calls the LLM requests
3. **Observe** — Adds tool results to context, loops back to Plan
4. **Stop** — When the LLM responds without tool calls, or `MaxIterations` is reached

```csharp
var provider = new OllamaAgentProvider(new OllamaOptions
{
    BaseUrl = "http://localhost:11434",
    DefaultModel = "qwen3:30b-instruct"
});

var registry = new ToolRegistry();
registry.Register(new WeatherToolProvider());
registry.Register(new DatabaseToolProvider());

var workflow = new WorkflowBuilder()
    .AgentLoop(provider, registry, options =>
    {
        options.MaxIterations = 15;
        options.SystemPrompt = "You are a helpful assistant with access to tools.";
        options.StepName = "Agent";
    })
    .Build();

await workflow.RunAsync(context);

// Results available in context
var response = context.Properties["Agent.Response"];      // Final LLM text
var iterations = context.Properties["Agent.Iterations"];  // Loop count
var toolResults = context.Properties["Agent.ToolResults"]; // List<ToolResult>
```

### AgentLoopOptions

| Property | Default | Description |
|----------|---------|-------------|
| `MaxIterations` | 10 | Maximum plan→act→observe cycles |
| `SystemPrompt` | null | System prompt for the agent |
| `ContextSources` | empty | `IContextSource` instances to inject |
| `Hooks` | null | `HookPipeline` for lifecycle interception |
| `ContextManager` | null | Custom `IContextManager` (default: `DefaultContextManager`) |
| `AutoCompact` | true | Auto-compact when over token threshold |
| `MaxContextTokens` | 100,000 | Token threshold for auto-compaction |
| `CompactionStrategy` | null | Custom `ICompactionStrategy` |
| `CompactionFocusInstructions` | null | Focus instructions for compaction summaries |
| `CheckpointStore` | null | `ICheckpointStore` for saving snapshots |
| `CheckpointInterval` | 1 | Save checkpoint every N iterations |

## ToolCallStep

For invoking a single known tool without the full agent loop. Supports `{PropertyName}` template substitution from context properties:

```csharp
var workflow = new WorkflowBuilder()
    .CallTool(registry, "get_weather", """{"city":"{CityName}"}""", stepName: "GetWeather")
    .Build();

context.Properties["CityName"] = "Austin";
await workflow.RunAsync(context);

var weatherResult = context.Properties["GetWeather.Result"];
var isError = context.Properties["GetWeather.IsError"];
```

## IContextSource & ContextAggregator

Context sources inject additional information into agent prompts — files, database records, API responses, or any structured data:

```csharp
public interface IContextSource
{
    string Name { get; }
    Task<IReadOnlyList<ContextDocument>> GetContextAsync(CancellationToken ct = default);
}
```

Each source returns `ContextDocument` objects with a name, content, source identifier, and metadata.

`ContextAggregator` combines multiple sources into a formatted prompt section:

```csharp
var aggregator = new ContextAggregator();
aggregator.Add(new FileContextSource("./docs"));
aggregator.Add(new DatabaseContextSource(connectionString));

// Use with AgentLoopStep
var workflow = new WorkflowBuilder()
    .AgentLoop(provider, registry, options =>
    {
        options.ContextSources.Add(new FileContextSource("./docs"));
        options.ContextSources.Add(new DatabaseContextSource(connectionString));
    })
    .Build();
```

The aggregator formats context as:

```markdown
## Context
### Document Name
Source: file://path
Content here...
```

## Builder Extensions

The fluent builder extensions make it easy to add agent steps:

```csharp
// Full agent loop
builder.AgentLoop(provider, registry, options => { ... });

// Single tool call
builder.CallTool(registry, "toolName", """{"arg":"{Value}"}""", stepName: "MyStep");
```

## DI Setup with AddAgentTooling()

Register agent tooling via dependency injection. All `IToolProvider` implementations in the container are auto-discovered:

```csharp
services.AddSingleton<IToolProvider, WeatherToolProvider>();
services.AddSingleton<IToolProvider, DatabaseToolProvider>();

services.AddAgentTooling(options =>
{
    options.MaxToolConcurrency = 4;
    options.DefaultTimeout = TimeSpan.FromSeconds(30);
});

// ToolRegistry is now available via DI with all providers registered
```

`AddAgentTooling()` registers:
- `AgentToolingOptions` as a singleton
- `ToolRegistry` as a singleton that auto-discovers all `IToolProvider` services

## Next Steps

- [Hook Lifecycle System](hooks.md) — Intercept and control agent execution
- [Context Compaction](compaction.md) — Manage long conversations
- [MCP Server Integration](mcp.md) — Connect to external tool servers
- [Agent Skills](skills.md) — Load skills from the filesystem
