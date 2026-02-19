# ASP.NET Core Hosting

The `Extensions.Hosting` package integrates WorkflowFramework with ASP.NET Core's hosting model.

## Installation

```bash
dotnet add package WorkflowFramework.Extensions.Hosting
```

## Quick Setup

```csharp
using WorkflowFramework.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Register core services + workflow registry
builder.Services.AddWorkflowFramework(opts =>
{
    opts.MaxParallelism = 4;
    opts.DefaultTimeout = TimeSpan.FromMinutes(5);
});

// Add scheduler as a BackgroundService
builder.Services.AddWorkflowHostedServices();

// Health checks
builder.Services.AddHealthChecks()
    .AddWorkflowHealthCheck();

var app = builder.Build();
app.MapHealthChecks("/health");
app.Run();
```

## WorkflowSchedulerHostedService

Runs the `IWorkflowScheduler` as a `BackgroundService`. It starts the scheduler on application startup and stops it gracefully on shutdown.

## WorkflowHealthCheck

Reports the number of registered workflows. Returns `Healthy` when the registry is available, `Degraded` otherwise.

## WorkflowHostingOptions

| Property | Default | Description |
|----------|---------|-------------|
| `MaxParallelism` | `Environment.ProcessorCount` | Maximum concurrent workflow executions |
| `DefaultTimeout` | `null` | Default timeout per step |
