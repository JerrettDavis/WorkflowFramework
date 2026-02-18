# Core Concepts

## Steps
A step is the fundamental unit of work. Implement `IStep`:

```csharp
public class MyStep : IStep
{
    public string Name => "MyStep";
    public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
}
```

## Context
`IWorkflowContext` carries state between steps via `Properties`, plus `WorkflowId`, `CorrelationId`, and `CancellationToken`.

## Middleware
`IWorkflowMiddleware` wraps step execution for cross-cutting concerns like logging, timing, and retry.

## Events
`IWorkflowEvents` provides lifecycle hooks: `OnWorkflowStarted`, `OnStepCompleted`, `OnWorkflowFailed`, etc.

## Results
`WorkflowResult` contains the final `Status` (Completed, Faulted, Aborted, Compensated) and the context.
