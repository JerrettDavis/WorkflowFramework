# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Entity Framework Core persistence provider
- Distributed workflow support (locking, queuing)
- Redis distributed lock and queue implementations
- ASP.NET Core hosting integration with health checks
- HTTP step support for making HTTP calls as workflow steps
- Roslyn analyzers for common workflow mistakes
- Comprehensive documentation site (docfx)
- Community files (CONTRIBUTING, CODE_OF_CONDUCT, SECURITY)
- Additional CI/CD workflows (CodeQL, dependency review, stale, labeler)
- Expanded benchmarks and samples
- 200+ tests

## [1.0.0] - 2026-02-18

### Added
- Core workflow engine with fluent builder API
- Strongly-typed workflows with `IWorkflow<TData>`
- Conditional branching (If/Then/Else)
- Parallel step execution
- Middleware pipeline (logging, timing, tracing, caching, audit)
- Saga/compensation pattern support
- Event hooks for workflow lifecycle
- Persistence/checkpointing (InMemory, SQLite)
- DI integration (Microsoft.Extensions.DependencyInjection)
- OpenTelemetry tracing
- Polly resilience integration
- Looping (ForEach, While, DoWhile, Retry)
- Try/Catch/Finally error handling
- Sub-workflows
- Typed pipelines (`IPipelineStep<TIn, TOut>`)
- Workflow registry with versioning
- Visualization (Mermaid, Graphviz)
- Scheduling (cron, delayed execution, approvals)
- Configuration (JSON/YAML workflow definitions)
- Testing utilities (harness, fake steps, event capture)
- Source generator for step discovery
- Multi-targeting: netstandard2.0, netstandard2.1, net8.0, net9.0, net10.0
