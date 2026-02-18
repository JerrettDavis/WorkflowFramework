# Getting Started

## Installation

```bash
dotnet add package WorkflowFramework
```

## Your First Workflow

```csharp
using WorkflowFramework;

var workflow = Workflow.Create("MyFirstWorkflow")
    .Step("Greet", ctx => {
        Console.WriteLine("Hello, WorkflowFramework!");
        return Task.CompletedTask;
    })
    .Build();

var result = await workflow.ExecuteAsync(new WorkflowContext());
Console.WriteLine(result.Status); // Completed
```

## Adding Multiple Steps

```csharp
var workflow = Workflow.Create("Pipeline")
    .Step("Validate", ctx => { /* validate */ return Task.CompletedTask; })
    .Step("Process", ctx => { /* process */ return Task.CompletedTask; })
    .Step("Save", ctx => { /* save */ return Task.CompletedTask; })
    .Build();
```
