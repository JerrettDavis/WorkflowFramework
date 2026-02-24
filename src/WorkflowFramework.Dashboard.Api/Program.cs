using WorkflowFramework.Dashboard.Api;
using WorkflowFramework.Dashboard.Api.Hubs;
using WorkflowFramework.Dashboard.Api.Services;
using WorkflowFramework.Dashboard.Api.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddSignalR();
builder.Services.AddSingleton<WorkflowExecutionNotifier>();
builder.Services.AddSingleton<WorkflowValidator>();
builder.Services.AddWorkflowDashboardApi();

// Opt-in to EF Core + SQLite persistence (replaces in-memory stores)
if (builder.Configuration.GetValue("Dashboard:UsePersistence", true))
{
    builder.Services.AddDashboardPersistence(
        builder.Configuration.GetConnectionString("DashboardDb"));
}

var app = builder.Build();

// Initialize database if persistence is enabled
if (app.Configuration.GetValue("Dashboard:UsePersistence", true))
{
    await app.Services.InitializeDashboardDatabaseAsync();
}

app.MapDefaultEndpoints();

app.MapHub<WorkflowExecutionHub>("/hubs/execution");
app.MapWorkflowDashboardApi();

app.MapGet("/health", () => "ok");

app.Run();
