using WorkflowFramework;
using WorkflowFramework.Extensions.Hosting;
using WorkflowFramework.Registry;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWorkflowFramework();
builder.Services.AddWorkflowHostedServices();
builder.Services.AddHealthChecks()
    .AddWorkflowHealthCheck();

var app = builder.Build();

// Register a sample workflow
var registry = app.Services.GetRequiredService<IWorkflowRegistry>();
registry.Register("hello", () => Workflow.Create("hello")
    .Step("Greet", ctx =>
    {
        ctx.Properties["message"] = "Hello from WorkflowFramework!";
        return Task.CompletedTask;
    })
    .Build());

app.MapHealthChecks("/health");

app.MapPost("/workflows/{name}/run", async (string name, IWorkflowRunner runner) =>
{
    var result = await runner.RunAsync(name, new WorkflowContext());
    result.Context.Properties.TryGetValue("message", out var message);
    return Results.Ok(new { result.Status, Message = message });
});

app.Run();
