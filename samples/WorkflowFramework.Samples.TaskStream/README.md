# 🌊 TaskStream — Intelligent Task Extraction & Orchestration

A comprehensive sample demonstrating WorkflowFramework's capabilities through an AI-powered task extraction and orchestration pipeline.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     TaskStream Orchestrator                      │
├─────────────────┬──────────────────────┬────────────────────────┤
│   Extraction    │       Triage         │        Report          │
│   Workflow      │       Workflow       │        Workflow        │
│                 │                      │                        │
│ ┌─────────────┐│ ┌──────────────────┐ │ ┌────────────────────┐ │
│ │CollectSource││ │    TriageStep    │ │ │ AggregateResults   │ │
│ │  Normalize  ││ │                  │ │ │  FormatMarkdown    │ │
│ │  Extract    ││ │  ┌────┐ ┌────┐  │ │ └────────────────────┘ │
│ │  Validate   ││ │  │Auto│ │Enri│  │ │                        │
│ │  Persist    ││ │  │Exec│ │ ch │  │ │                        │
│ └─────────────┘│ │  └────┘ └────┘  │ │                        │
│                 │ └──────────────────┘ │                        │
└─────────────────┴──────────────────────┴────────────────────────┘
        ▲                                         │
        │                                         ▼
  ┌───────────┐  ┌───────────┐  ┌────────┐  ┌──────────┐
  │ InMemory  │  │   Email   │  │Webhook │  │   File   │
  │  Source   │  │  Source   │  │ Source │  │ Watcher  │
  └───────────┘  └───────────┘  └────────┘  └──────────┘
```

### Data Flow

```
Sources → SourceMessages → Normalize → Extract → TodoItems → Validate
  → Persist → Triage → [Automatable | Human] → Parallel Execution
  → Aggregate → Markdown Report
```

## Quick Start

```bash
dotnet run --project samples/WorkflowFramework.Samples.TaskStream/ --framework net10.0
```

## Features Demonstrated

| Feature | Where |
|---------|-------|
| Fluent workflow builder | `Workflows/*.cs` |
| Sub-workflow composition | `TaskStreamOrchestrator.cs` |
| Parallel step execution | `TriageWorkflow.cs` |
| AI agent abstraction | `TaskStreamAgentProvider.cs` |
| Dependency injection | `ServiceCollectionExtensions.cs` |
| Hook/plugin pattern | `Hooks/ITodoHook.cs` |
| Multiple input sources | `Sources/ITaskSource.cs` |
| Store abstraction | `Store/ITodoStore.cs` |

## Webhook Source

Start the app, then POST messages via curl:

```bash
# The WebhookTaskSource uses a Channel<T> — wire it to a minimal API endpoint:
curl -X POST http://localhost:5000/api/tasks \
  -H "Content-Type: application/json" \
  -d '{"content": "Deploy hotfix to production, review security audit"}'
```

## File Watcher Source

Create an `inbox/` directory and drop text files:

```bash
mkdir inbox
echo "Schedule dentist appointment, buy groceries" > inbox/tasks.txt
```

The `FileWatcherTaskSource` watches for new `.txt`/`.md` files and processes them automatically.

## Todoist Integration

Configure via `TodoistOptions` in DI:

```csharp
services.Configure<TodoistOptions>(o =>
{
    o.ApiKey = "your-todoist-api-key";
    o.ProjectId = "your-project-id";
});
services.AddSingleton<ITodoHook, TodoistHook>();
```

Tasks will be synced to Todoist on creation and marked complete when automated.

## Plugging In a Real LLM

Replace `TaskStreamAgentProvider` with a real `IAgentProvider` implementation:

```csharp
// Example with a custom Semantic Kernel provider:
services.AddSingleton<IAgentProvider>(sp =>
    new SemanticKernelAgentProvider(kernel));
```

The pipeline will use the real LLM for extraction, triage, execution, and enrichment — no other code changes needed.

## Extension Points

- **Custom Sources** — Implement `ITaskSource` for Slack, Teams, RSS, etc.
- **Custom Stores** — Implement `ITodoStore` for SQL, Redis, etc.
- **Custom Hooks** — Implement `ITodoHook` for notifications, syncing, logging
- **Custom Tools** — Implement `IAgentTool` for real web search, calendar APIs, etc.
- **Custom Agent** — Implement `IAgentProvider` for OpenAI, Anthropic, local models

## Project Structure

```
├── Program.cs                  — Entry point & demo runner
├── Models/                     — Domain models (TodoItem, SourceMessage, etc.)
├── Sources/                    — Input sources (InMemory, Email, Webhook, FileWatcher)
├── Store/                      — Persistence (InMemory, JsonFile)
├── Hooks/                      — Lifecycle hooks (Console, Webhook, Todoist)
├── Tools/                      — Agent tools (Search, Calendar, Location, Deploy)
├── Agents/                     — AI agent provider (rule-based mock)
├── Steps/                      — Workflow steps (10 steps across the pipeline)
├── Workflows/                  — Workflow definitions & orchestrator
└── Extensions/                 — DI registration
```
