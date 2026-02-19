# Resilience (Polly Integration)

The `Extensions.Polly` package adds [Polly](https://github.com/App-vNext/Polly) resilience pipelines as workflow middleware.

## Installation

```bash
dotnet add package WorkflowFramework.Extensions.Polly
```

## Usage

### Via Builder Extension

```csharp
using WorkflowFramework.Extensions.Polly;

var workflow = new WorkflowBuilder()
    .UseResilience(pipeline =>
    {
        pipeline
            .AddRetry(new()
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential
            })
            .AddTimeout(TimeSpan.FromSeconds(30));
    })
    .Step(httpCallStep)
    .Step(dbWriteStep)
    .Build();
```

### With a Pre-Built Pipeline

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new() { MaxRetryAttempts = 5 })
    .AddCircuitBreaker(new()
    {
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(10),
        MinimumThroughput = 8
    })
    .Build();

var workflow = new WorkflowBuilder()
    .UseResilience(pipeline)
    .Step(externalServiceStep)
    .Build();
```

## How It Works

`ResilienceMiddleware` implements `IWorkflowMiddleware` and wraps each step's execution inside the Polly `ResiliencePipeline.ExecuteAsync` method. This means retries, circuit breakers, timeouts, rate limiters, and hedging all apply transparently to every step in the workflow.

> [!TIP]
> You can add multiple `UseResilience` calls with different pipelines for different sections of your workflow.
