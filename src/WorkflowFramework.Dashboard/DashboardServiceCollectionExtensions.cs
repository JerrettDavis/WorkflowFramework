using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Dashboard.Services;

namespace WorkflowFramework.Dashboard;

/// <summary>
/// Extension methods for registering the workflow dashboard.
/// </summary>
public static class DashboardServiceCollectionExtensions
{
    /// <summary>
    /// Adds the workflow dashboard services to the service collection.
    /// </summary>
    public static IServiceCollection AddWorkflowDashboard(this IServiceCollection services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        services.AddScoped<WorkflowDashboardService>();
        return services;
    }

    /// <summary>
    /// Maps the workflow dashboard Razor components at the specified path prefix.
    /// </summary>
    public static IEndpointRouteBuilder MapWorkflowDashboard(
        this IEndpointRouteBuilder endpoints,
        string pathPrefix = "/workflows")
    {
        if (endpoints is null) throw new ArgumentNullException(nameof(endpoints));

        var group = endpoints.MapGroup(pathPrefix);

        // Workflow list
        group.MapGet("/", async (WorkflowDashboardService svc, CancellationToken ct) =>
        {
            var workflows = await svc.GetWorkflowsAsync(ct);
            return Results.Ok(workflows);
        });

        // Workflow detail
        group.MapGet("/{name}", (WorkflowDashboardService svc, string name) =>
        {
            try
            {
                var detail = svc.GetWorkflowDetail(name);
                return Results.Ok(detail);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        // Trigger workflow run
        group.MapPost("/{name}/run", async (WorkflowDashboardService svc, string name, CancellationToken ct) =>
        {
            try
            {
                var result = await svc.TriggerRunAsync(name, ct);
                return Results.Ok(new { result.Status, result.IsSuccess });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        // Run history (optionally filtered by workflow name)
        group.MapGet("/runs", async (WorkflowDashboardService svc, string? workflowName, int? max, CancellationToken ct) =>
        {
            var runs = await svc.GetRunsAsync(workflowName, max, ct);
            return Results.Ok(runs);
        });

        // Run detail
        group.MapGet("/runs/{runId}", async (WorkflowDashboardService svc, string runId, CancellationToken ct) =>
        {
            var run = await svc.GetRunAsync(runId, ct);
            return run is null ? Results.NotFound() : Results.Ok(run);
        });

        return endpoints;
    }
}
