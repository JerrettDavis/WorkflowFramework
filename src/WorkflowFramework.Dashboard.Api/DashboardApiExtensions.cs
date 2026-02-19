using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Services;

namespace WorkflowFramework.Dashboard.Api;

/// <summary>
/// Extension methods to register dashboard API services and map endpoints.
/// </summary>
public static class DashboardApiExtensions
{
    /// <summary>
    /// Adds dashboard API services to the service collection.
    /// </summary>
    public static IServiceCollection AddWorkflowDashboardApi(this IServiceCollection services)
    {
        services.AddSingleton<IWorkflowDefinitionStore, InMemoryWorkflowDefinitionStore>();
        services.AddSingleton(StepTypeRegistry.CreateDefault());
        services.AddSingleton<WorkflowRunService>();
        return services;
    }

    /// <summary>
    /// Maps all dashboard API endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapWorkflowDashboardApi(this IEndpointRouteBuilder endpoints)
    {
        MapWorkflowEndpoints(endpoints);
        MapStepEndpoints(endpoints);
        MapRunEndpoints(endpoints);
        MapPluginEndpoints(endpoints);
        return endpoints;
    }

    private static void MapWorkflowEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/workflows").WithTags("Workflows");

        group.MapGet("/", async (IWorkflowDefinitionStore store, CancellationToken ct) =>
        {
            var workflows = await store.GetAllAsync(ct);
            return Results.Ok(workflows.Select(w => new WorkflowListItem
            {
                Id = w.Id,
                Name = w.Definition.Name,
                Description = w.Description,
                LastModified = w.LastModified,
                StepCount = w.Definition.Steps.Count
            }));
        }).WithName("ListWorkflows");

        group.MapGet("/{id}", async (string id, IWorkflowDefinitionStore store, CancellationToken ct) =>
        {
            var workflow = await store.GetByIdAsync(id, ct);
            return workflow is null ? Results.NotFound() : Results.Ok(workflow);
        }).WithName("GetWorkflow");

        group.MapPost("/", async (CreateWorkflowRequest request, IWorkflowDefinitionStore store, CancellationToken ct) =>
        {
            if (request.Definition is null)
                return Results.BadRequest("Definition is required.");

            var created = await store.CreateAsync(request, ct);
            return Results.Created($"/api/workflows/{created.Id}", created);
        }).WithName("CreateWorkflow");

        group.MapPut("/{id}", async (string id, CreateWorkflowRequest request, IWorkflowDefinitionStore store, CancellationToken ct) =>
        {
            if (request.Definition is null)
                return Results.BadRequest("Definition is required.");

            var updated = await store.UpdateAsync(id, request, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }).WithName("UpdateWorkflow");

        group.MapDelete("/{id}", async (string id, IWorkflowDefinitionStore store, CancellationToken ct) =>
        {
            var deleted = await store.DeleteAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).WithName("DeleteWorkflow");

        group.MapPost("/{id}/duplicate", async (string id, IWorkflowDefinitionStore store, CancellationToken ct) =>
        {
            var duplicate = await store.DuplicateAsync(id, ct);
            return duplicate is null ? Results.NotFound() : Results.Created($"/api/workflows/{duplicate.Id}", duplicate);
        }).WithName("DuplicateWorkflow");

        group.MapPost("/{id}/run", async (string id, WorkflowRunService runService, CancellationToken ct) =>
        {
            var run = await runService.StartRunAsync(id, ct);
            return run is null ? Results.NotFound() : Results.Ok(run);
        }).WithName("RunWorkflow");
    }

    private static void MapStepEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/steps").WithTags("Steps");

        group.MapGet("/", (StepTypeRegistry registry) =>
        {
            return Results.Ok(registry.All);
        }).WithName("ListStepTypes");

        group.MapGet("/{type}", (string type, StepTypeRegistry registry) =>
        {
            var info = registry.Get(type);
            return info is null ? Results.NotFound() : Results.Ok(info);
        }).WithName("GetStepType");
    }

    private static void MapRunEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/runs").WithTags("Runs");

        group.MapGet("/", async (int? limit, WorkflowRunService runService, CancellationToken ct) =>
        {
            var runs = await runService.GetRunsAsync(limit, ct);
            return Results.Ok(runs);
        }).WithName("ListRuns");

        group.MapGet("/{runId}", async (string runId, WorkflowRunService runService, CancellationToken ct) =>
        {
            var run = await runService.GetRunAsync(runId, ct);
            return run is null ? Results.NotFound() : Results.Ok(run);
        }).WithName("GetRun");

        group.MapDelete("/{runId}", async (string runId, WorkflowRunService runService, CancellationToken ct) =>
        {
            var cancelled = await runService.CancelRunAsync(runId, ct);
            return cancelled ? Results.NoContent() : Results.NotFound();
        }).WithName("CancelRun");
    }

    private static void MapPluginEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/plugins", () =>
        {
            // Return static plugin list; in a real impl this would scan loaded assemblies
            var plugins = new[]
            {
                new PluginInfo { Name = "WorkflowFramework.Core", Version = "1.0.0", Description = "Core workflow engine" },
                new PluginInfo { Name = "WorkflowFramework.Integration", Version = "1.0.0", Description = "Enterprise integration patterns" },
                new PluginInfo { Name = "WorkflowFramework.AI", Version = "1.0.0", Description = "AI and agent step types" },
                new PluginInfo { Name = "WorkflowFramework.DataMapping", Version = "1.0.0", Description = "Data transformation and validation" },
                new PluginInfo { Name = "WorkflowFramework.Http", Version = "1.0.0", Description = "HTTP and webhook steps" },
                new PluginInfo { Name = "WorkflowFramework.Events", Version = "1.0.0", Description = "Event publishing and subscription" },
                new PluginInfo { Name = "WorkflowFramework.HumanTasks", Version = "1.0.0", Description = "Human task and approval steps" }
            };
            return Results.Ok(plugins);
        }).WithTags("Plugins").WithName("ListPlugins");

        endpoints.MapGet("/api/connectors", () =>
        {
            var connectors = new[]
            {
                new ConnectorInfo { Name = "HTTP", Type = "http", Description = "Generic HTTP/REST connector" },
                new ConnectorInfo { Name = "gRPC", Type = "grpc", Description = "gRPC service connector" },
                new ConnectorInfo { Name = "Message Queue", Type = "messaging", Description = "Message queue connector (RabbitMQ, Kafka, etc.)" },
                new ConnectorInfo { Name = "Database", Type = "database", Description = "Database connector" },
                new ConnectorInfo { Name = "File System", Type = "filesystem", Description = "File system connector" }
            };
            return Results.Ok(connectors);
        }).WithTags("Connectors").WithName("ListConnectors");
    }
}
