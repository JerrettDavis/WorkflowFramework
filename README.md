# WorkflowFramework

[![CI](https://github.com/JerrettDavis/WorkflowFramework/actions/workflows/ci.yml/badge.svg)](https://github.com/JerrettDavis/WorkflowFramework/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/JerrettDavis/WorkflowFramework/graph/badge.svg)](https://codecov.io/gh/JerrettDavis/WorkflowFramework)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A fluent, extensible workflow/pipeline engine for .NET with async-first design, middleware, branching, parallel execution, saga/compensation, and rich extensibility.

## Features

- **Fluent Builder API** — chain steps naturally with a clean DSL
- **Strongly-Typed Context** — type-safe data flowing through your pipeline
- **Conditional Branching** — `If/Then/Else` for decision-based workflows
- **Parallel Execution** — run steps concurrently with `Parallel()`
- **Middleware Pipeline** — logging, retry, timing, tracing, and custom interceptors
- **Saga/Compensation** — automatic rollback on failure
- **Event Hooks** — `OnStepStarted`, `OnStepCompleted`, `OnWorkflowFailed`, etc.
- **Persistence/Checkpointing** — save and resume long-running workflows
- **DI Integration** — first-class Microsoft.Extensions.DependencyInjection support
- **OpenTelemetry** — built-in tracing and timing diagnostics
- **Polly Integration** — resilience policies via Polly v8
- **Multi-targeting** — netstandard2.0, netstandard2.1, net8.0, net9.0, net10.0

## Quick Start

### Install

```bash
dotnet add package WorkflowFramework
```

### Define Steps

```csharp
public class ValidateInput : IStep
{
    public string Name => "ValidateInput";

    public Task ExecuteAsync(IWorkflowContext context)
    {
        // validation logic
        return Task.CompletedTask;
    }
}

public class ProcessData : IStep
{
    public string Name => "ProcessData";

    public Task ExecuteAsync(IWorkflowContext context)
    {
        // processing logic
        return Task.CompletedTask;
    }
}
```

### Build & Execute

```csharp
var workflow = Workflow.Create("MyWorkflow")
    .Step<ValidateInput>()
    .Step<ProcessData>()
    .Step("SaveResult", ctx =>
    {
        Console.WriteLine("Saved!");
        return Task.CompletedTask;
    })
    .Build();

var result = await workflow.ExecuteAsync(new WorkflowContext());
Console.WriteLine(result.Status); // Completed
```

## Typed Workflows

```csharp
public class OrderData
{
    public string OrderId { get; set; } = "";
    public decimal Total { get; set; }
    public bool IsValid { get; set; }
}

public class ValidateOrder : IStep<OrderData>
{
    public string Name => "ValidateOrder";

    public Task ExecuteAsync(IWorkflowContext<OrderData> context)
    {
        context.Data.IsValid = context.Data.Total > 0;
        return Task.CompletedTask;
    }
}

var workflow = Workflow.Create<OrderData>("OrderPipeline")
    .Step(new ValidateOrder())
    .If(ctx => ctx.Data.IsValid)
        .Then(new ProcessOrder())
        .Else(new RejectOrder())
    .Build();

var result = await workflow.ExecuteAsync(
    new WorkflowContext<OrderData>(new OrderData { OrderId = "ORD-1", Total = 99.99m }));

Console.WriteLine(result.Data.IsValid); // true
```

## Conditional Branching

```csharp
Workflow.Create()
    .If(ctx => someCondition)
        .Then<ProcessStep>()
        .Else<RejectStep>()
    .Step<FinalStep>()
    .Build();
```

## Parallel Execution

```csharp
Workflow.Create()
    .Parallel(p => p
        .Step<SendEmail>()
        .Step<SendSms>()
        .Step<UpdateDashboard>())
    .Build();
```

## Middleware

```csharp
public class LoggingMiddleware : IWorkflowMiddleware
{
    public async Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
    {
        Console.WriteLine($"Starting: {step.Name}");
        await next(context);
        Console.WriteLine($"Completed: {step.Name}");
    }
}

Workflow.Create()
    .Use<LoggingMiddleware>()
    .Step<MyStep>()
    .Build();
```

## Saga/Compensation

```csharp
public class DebitAccount : ICompensatingStep
{
    public string Name => "DebitAccount";

    public Task ExecuteAsync(IWorkflowContext context) { /* debit */ return Task.CompletedTask; }
    public Task CompensateAsync(IWorkflowContext context) { /* credit back */ return Task.CompletedTask; }
}

Workflow.Create()
    .WithCompensation()
    .Step(new DebitAccount())
    .Step(new CreditAccount())
    .Build();
```

## Event Hooks

```csharp
Workflow.Create()
    .WithEvents(new MyEventHandler())
    .Step<MyStep>()
    .Build();

public class MyEventHandler : WorkflowEventsBase
{
    public override Task OnStepStartedAsync(IWorkflowContext ctx, IStep step)
    {
        Console.WriteLine($"Step started: {step.Name}");
        return Task.CompletedTask;
    }
}
```

## Extensions

| Package | Description |
|---------|-------------|
| `WorkflowFramework` | Core abstractions + fluent builder |
| `WorkflowFramework.Extensions.DependencyInjection` | Microsoft DI integration |
| `WorkflowFramework.Extensions.Polly` | Polly resilience policies |
| `WorkflowFramework.Extensions.Persistence` | Checkpoint/state persistence abstractions |
| `WorkflowFramework.Extensions.Persistence.InMemory` | In-memory state store |
| `WorkflowFramework.Extensions.Diagnostics` | OpenTelemetry tracing + timing |
| `WorkflowFramework.Generators` | Source generators for step discovery |

### Polly Integration

```csharp
using WorkflowFramework.Extensions.Polly;

Workflow.Create()
    .UseResilience(builder => builder
        .AddRetry(new RetryStrategyOptions { MaxRetryAttempts = 3 }))
    .Step<UnreliableStep>()
    .Build();
```

### Diagnostics

```csharp
using WorkflowFramework.Extensions.Diagnostics;

Workflow.Create()
    .Use<TracingMiddleware>()   // OpenTelemetry spans
    .Use<TimingMiddleware>()    // Step timing
    .Step<MyStep>()
    .Build();
```

### Persistence

```csharp
using WorkflowFramework.Extensions.Persistence;
using WorkflowFramework.Extensions.Persistence.InMemory;

var store = new InMemoryWorkflowStateStore();

Workflow.Create()
    .Use(new CheckpointMiddleware(store))
    .Step<LongRunningStep>()
    .Build();
```

### Dependency Injection

```csharp
using WorkflowFramework.Extensions.DependencyInjection;

services.AddWorkflowFramework();
services.AddStep<ValidateInput>();
services.AddWorkflowMiddleware<LoggingMiddleware>();
```

## Building

```bash
dotnet build WorkflowFramework.slnx
dotnet test WorkflowFramework.slnx
```

## License

[MIT](LICENSE) © JDH Productions LLC.
