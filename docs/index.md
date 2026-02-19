---
_layout: landing
---

# WorkflowFramework

[![Build](https://github.com/JerrettDavis/WorkflowFramework/actions/workflows/docs.yml/badge.svg)](https://github.com/JerrettDavis/WorkflowFramework/actions)
[![NuGet](https://img.shields.io/nuget/v/WorkflowFramework.svg)](https://www.nuget.org/packages/WorkflowFramework)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/JerrettDavis/WorkflowFramework/blob/main/LICENSE)

A fluent, extensible workflow and pipeline engine for .NET.

## Getting Started

- [Getting Started](user-guide/getting-started.md) — Installation and first workflow
- [Core Concepts](user-guide/core-concepts.md) — Steps, context, middleware, events

## Building Workflows

- [Fluent Builder](user-guide/fluent-builder.md) — Complete builder API
- [Branching](user-guide/branching.md) — Conditional logic
- [Loops](user-guide/loops.md) — Iteration patterns
- [Parallel Execution](user-guide/parallel.md) — Run steps concurrently
- [Typed Pipelines](user-guide/typed-pipelines.md) — Strongly-typed workflows
- [Dynamic Expressions](user-guide/expressions.md) — Runtime expression evaluation and templates

## Reliability & Operations

- [Error Handling](user-guide/error-handling.md) — Try/Catch/Finally, compensation
- [Middleware](user-guide/middleware.md) — Interceptors and cross-cutting concerns
- [Resilience (Polly)](user-guide/resilience.md) — Retry, circuit breaker, timeout
- [Persistence](user-guide/persistence.md) — Checkpointing and state stores
- [Scheduling](user-guide/scheduling.md) — Cron and interval scheduling
- [Hosting](user-guide/hosting.md) — ASP.NET Core integration and health checks

## Integration & Data

- [Enterprise Integration Patterns](user-guide/integration-patterns.md) — Router, Splitter, Aggregator, Scatter-Gather, and 15+ more EIP steps
- [Connectors](user-guide/connectors.md) — HTTP, SQL, FTP, gRPC, messaging (RabbitMQ, Kafka, Azure SB)
- [Data Mapping](user-guide/data-mapping.md) — Declarative field mapping, format conversion, schema validation
- [Event-Driven Workflows](user-guide/events.md) — Publish/subscribe, event correlation
- [Reactive Extensions](user-guide/reactive.md) — Streaming steps with `IAsyncEnumerable<T>`

## AI & Human Tasks

- [AI Agents](user-guide/ai-agents.md) — LLM calls, AI routing decisions, autonomous planning (Ollama, Semantic Kernel)
- [Human-in-the-Loop](user-guide/human-tasks.md) — Approval gates, task inbox, escalation, delegation

## Advanced

- [Configuration](user-guide/configuration.md) — YAML/JSON workflow definitions
- [Dependency Injection](user-guide/dependency-injection.md) — DI container integration
- [Diagnostics](user-guide/diagnostics.md) — Tracing and observability
- [Extensibility](user-guide/extensibility.md) — Custom steps and middleware
- [Validation](user-guide/validation.md) — Workflow validation rules
- [Versioning](user-guide/versioning.md) — Schema versioning
- [Visualization](user-guide/visualization.md) — Workflow graph rendering
- [Testing](user-guide/testing.md) — Test harness and utilities

## Architecture

WorkflowFramework is built around a simple pipeline model:

```
Steps → Middleware → Context → Runner
```

**Steps** are units of work. **Middleware** wraps step execution (logging, retry, auth). **Context** carries data through the pipeline. The **Runner** orchestrates execution. Everything is interface-based and extensible via DI.

## Packages

| Package | Description |
|---------|-------------|
| `WorkflowFramework` | Core engine, builder, runner |
| `WorkflowFramework.Extensions.AI` | LLM/Agent integration |
| `WorkflowFramework.Extensions.Integration` | Enterprise Integration Patterns |
| `WorkflowFramework.Extensions.DataMapping` | Data transformation engine |
| `WorkflowFramework.Extensions.Connectors.*` | External system connectors |
| `WorkflowFramework.Extensions.Events` | Event bus and event steps |
| `WorkflowFramework.Extensions.Expressions` | Dynamic expressions and templates |
| `WorkflowFramework.Extensions.HumanTasks` | Human-in-the-loop tasks |
| `WorkflowFramework.Extensions.Hosting` | ASP.NET Core hosting |
| `WorkflowFramework.Extensions.Polly` | Polly resilience |
| `WorkflowFramework.Extensions.Reactive` | Streaming/reactive steps |
| `WorkflowFramework.Extensions.Persistence.*` | State persistence |
| `WorkflowFramework.Extensions.Scheduling` | Cron/interval scheduling |
| `WorkflowFramework.Extensions.DependencyInjection` | DI registration |
| `WorkflowFramework.Extensions.Diagnostics` | Tracing and observability |
| `WorkflowFramework.Extensions.Visualization` | Graph rendering |
| `WorkflowFramework.Testing` | Test utilities |
| `WorkflowFramework.Analyzers` | Roslyn analyzers |
