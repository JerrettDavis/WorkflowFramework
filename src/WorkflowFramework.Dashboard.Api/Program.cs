using WorkflowFramework.Dashboard.Api.Hubs;
using WorkflowFramework.Dashboard.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSignalR();
builder.Services.AddSingleton<WorkflowExecutionNotifier>();
builder.Services.AddSingleton<WorkflowValidator>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapHub<WorkflowExecutionHub>("/hubs/execution");

app.MapGet("/health", () => "ok");

app.Run();
