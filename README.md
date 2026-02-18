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
- **Looping** — `ForEach`, `While`, `DoWhile`, `Retry` for iteration patterns
- **Error Handling DSL** — `Try/Catch/Finally` blocks in your workflow
- **Sub-Workflows** — compose workflows from smaller workflows
- **Typed Pipelines** — `IPipelineStep<TIn, TOut>` with chained input/output types
- **Workflow Registry** — register and resolve workflows by name with versioning
- **Step Attributes** — `[StepName]`, `[StepTimeout]`, `[StepRetry]`, `[StepOrder]`
- **Validation** — `IWorkflowValidator` / `IStepValidator` for pre-execution validation
- **Visualization** — export workflows to Mermaid and DOT/Graphviz diagrams
- **Scheduling** — cron expressions and delayed execution
- **Approval Steps** — human interaction with `IApprovalService`
- **Audit Trail** — `AuditMiddleware` with `IAuditStore`
- **Testing Utilities** — `WorkflowTestHarness`, `FakeStep`, `StepTestBuilder`
- **Configuration DSL** — define workflows in JSON (YAML coming soon)
- **SQLite Persistence** — durable state store via SQLite

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
| `WorkflowFramework.Extensions.Configuration` | JSON/YAML workflow definitions |
| `WorkflowFramework.Extensions.Scheduling` | Cron scheduling, approvals, delayed execution |
| `WorkflowFramework.Extensions.Visualization` | Mermaid + DOT diagram export |
| `WorkflowFramework.Extensions.Reactive` | Async streams / `IAsyncEnumerable` support |
| `WorkflowFramework.Extensions.Persistence.Sqlite` | SQLite state store |
| `WorkflowFramework.Testing` | Test harness, fake steps, event capture |

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

### Looping & Iteration

```csharp
using WorkflowFramework.Builder;

Workflow.Create()
    .ForEach<string>(
        ctx => (List<string>)ctx.Properties["Items"]!,
        body => body.Step("Process", ctx =>
        {
            var item = (string)ctx.Properties["ForEach.Current"]!;
            Console.WriteLine($"Processing: {item}");
            return Task.CompletedTask;
        }))
    .Build();

// While loop
Workflow.Create()
    .While(ctx => (int)ctx.Properties["Count"]! < 10,
        body => body.Step("Increment", ctx =>
        {
            ctx.Properties["Count"] = (int)ctx.Properties["Count"]! + 1;
            return Task.CompletedTask;
        }))
    .Build();

// Retry group
Workflow.Create()
    .Retry(body => body.Step<FlakyStep>(), maxAttempts: 3)
    .Build();
```

### Try/Catch/Finally

```csharp
Workflow.Create()
    .Try(body => body.Step<RiskyStep>())
    .Catch<InvalidOperationException>((ctx, ex) =>
    {
        Console.WriteLine($"Caught: {ex.Message}");
        return Task.CompletedTask;
    })
    .Finally(body => body.Step("Cleanup", _ => { /* cleanup */ return Task.CompletedTask; }))
    .Build();
```

### Sub-Workflows

```csharp
var validation = Workflow.Create("Validation")
    .Step<ValidateInput>()
    .Step<ValidatePermissions>()
    .Build();

var main = Workflow.Create("Main")
    .SubWorkflow(validation)
    .Step<ProcessData>()
    .Build();
```

### Typed Pipeline

```csharp
using WorkflowFramework.Pipeline;

var pipeline = Pipeline.Create<string>()
    .Pipe<int>((s, ct) => Task.FromResult(int.Parse(s)))
    .Pipe<string>((n, ct) => Task.FromResult($"Value: {n * 2}"))
    .Build();

var result = await pipeline("21", CancellationToken.None); // "Value: 42"
```

### Workflow Registry & Versioning

```csharp
using WorkflowFramework.Registry;
using WorkflowFramework.Versioning;

var registry = new WorkflowRegistry();
registry.Register("OrderProcessing", () => BuildOrderWorkflow());

var runner = new WorkflowRunner(registry);
var result = await runner.RunAsync("OrderProcessing", context);

// Versioned workflows
var versionedRegistry = new VersionedWorkflowRegistry();
versionedRegistry.Register("OrderProcessing", 1, () => BuildV1());
versionedRegistry.Register("OrderProcessing", 2, () => BuildV2());
var latest = versionedRegistry.Resolve("OrderProcessing"); // Returns v2
```

### Visualization

```csharp
using WorkflowFramework.Extensions.Visualization;

var workflow = Workflow.Create("MyWorkflow")
    .Step<StepA>()
    .Step<StepB>()
    .Build();

Console.WriteLine(workflow.ToMermaid());  // Mermaid diagram
Console.WriteLine(workflow.ToDot());      // Graphviz DOT format
```

### JSON Configuration

```csharp
using WorkflowFramework.Extensions.Configuration;

var loader = new JsonWorkflowDefinitionLoader();
var definition = loader.LoadFromFile("workflow.json");

var stepRegistry = new StepRegistry();
stepRegistry.Register<ValidateOrder>();
stepRegistry.Register<ChargePayment>();

var builder = new WorkflowDefinitionBuilder(stepRegistry);
var workflow = builder.Build(definition);
```

### Testing

```csharp
using WorkflowFramework.Testing;

// Override specific steps in tests
var harness = new WorkflowTestHarness()
    .OverrideStep("ChargePayment", ctx =>
    {
        ctx.Properties["PaymentCharged"] = true;
        return Task.CompletedTask;
    });

var result = await harness.ExecuteAsync(workflow, new WorkflowContext());

// Capture events for assertions
var events = new InMemoryWorkflowEvents();
var workflow = Workflow.Create()
    .WithEvents(events)
    .Step<MyStep>()
    .Build();

await workflow.ExecuteAsync(new WorkflowContext());
Assert.Single(events.StepCompleted);
```

## Building

```bash
dotnet build WorkflowFramework.slnx
dotnet test WorkflowFramework.slnx
```

## License

[MIT](LICENSE) © JDH Productions LLC.
