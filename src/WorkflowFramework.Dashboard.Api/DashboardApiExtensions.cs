using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Services;
using WorkflowFramework.Serialization;

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
        services.AddSingleton<WorkflowDefinitionCompiler>();
        services.AddSingleton<WorkflowRunService>();
        services.AddSingleton<IWorkflowTemplateLibrary, InMemoryWorkflowTemplateLibrary>();
        services.AddSingleton<IWorkflowVersioningService, WorkflowVersioningService>();
        services.AddSingleton<IAuditTrailService, AuditTrailService>();
        services.AddSingleton<IDashboardSettingsService, DashboardSettingsService>();
        services.AddHttpClient("OllamaClient");
        return services;
    }

    /// <summary>
    /// Maps all dashboard API endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapWorkflowDashboardApi(this IEndpointRouteBuilder endpoints)
    {
        // Seed sample workflows on first call
        var store = endpoints.ServiceProvider.GetRequiredService<IWorkflowDefinitionStore>();
        SampleWorkflowSeeder.SeedAsync(store).GetAwaiter().GetResult();

        MapWorkflowEndpoints(endpoints);
        MapStepEndpoints(endpoints);
        MapRunEndpoints(endpoints);
        MapPluginEndpoints(endpoints);
        MapTemplateEndpoints(endpoints);
        MapVersionEndpoints(endpoints);
        MapAuditEndpoints(endpoints);
        MapTagEndpoints(endpoints);
        MapValidationEndpoints(endpoints);
        MapSettingsEndpoints(endpoints);
        return endpoints;
    }

    private static void MapWorkflowEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/workflows").WithTags("Workflows");

        group.MapGet("/", async (string? search, string? tags, IWorkflowDefinitionStore store, CancellationToken ct) =>
        {
            var workflows = await store.GetAllAsync(ct);
            IEnumerable<SavedWorkflowDefinition> filtered = workflows;

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                filtered = filtered.Where(w =>
                    w.Definition.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (w.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    w.Tags.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase)));
            }

            if (!string.IsNullOrWhiteSpace(tags))
            {
                var tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                filtered = filtered.Where(w => tagList.All(t => w.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)));
            }

            return Results.Ok(filtered.Select(w => new WorkflowListItem
            {
                Id = w.Id,
                Name = w.Definition.Name,
                Description = w.Description,
                Tags = w.Tags,
                LastModified = w.LastModified,
                StepCount = w.Definition.Steps.Count
            }));
        }).WithName("ListWorkflows");

        group.MapGet("/{id}", async (string id, IWorkflowDefinitionStore store, CancellationToken ct) =>
        {
            var workflow = await store.GetByIdAsync(id, ct);
            return workflow is null ? Results.NotFound() : Results.Ok(workflow);
        }).WithName("GetWorkflow");

        group.MapPost("/", async (CreateWorkflowRequest request, IWorkflowDefinitionStore store, IWorkflowVersioningService versioning, IAuditTrailService audit, HttpContext http, CancellationToken ct) =>
        {
            if (request.Definition is null)
                return Results.BadRequest("Definition is required.");

            var created = await store.CreateAsync(request, ct);
            versioning.CreateVersion(created, changeSummary: "Initial version");
            audit.Log("workflow.created", created.Id, $"Created workflow '{created.Definition.Name}'", ipAddress: http.Connection.RemoteIpAddress?.ToString());
            return Results.Created($"/api/workflows/{created.Id}", created);
        }).WithName("CreateWorkflow");

        group.MapPut("/{id}", async (string id, CreateWorkflowRequest request, IWorkflowDefinitionStore store, IWorkflowVersioningService versioning, IAuditTrailService audit, HttpContext http, CancellationToken ct) =>
        {
            if (request.Definition is null)
                return Results.BadRequest("Definition is required.");

            var updated = await store.UpdateAsync(id, request, ct);
            if (updated is null) return Results.NotFound();
            versioning.CreateVersion(updated);
            audit.Log("workflow.updated", id, $"Updated workflow '{updated.Definition.Name}'", ipAddress: http.Connection.RemoteIpAddress?.ToString());
            return Results.Ok(updated);
        }).WithName("UpdateWorkflow");

        group.MapDelete("/{id}", async (string id, IWorkflowDefinitionStore store, IAuditTrailService audit, HttpContext http, CancellationToken ct) =>
        {
            var deleted = await store.DeleteAsync(id, ct);
            if (!deleted) return Results.NotFound();
            audit.Log("workflow.deleted", id, $"Deleted workflow '{id}'", ipAddress: http.Connection.RemoteIpAddress?.ToString());
            return Results.NoContent();
        }).WithName("DeleteWorkflow");

        group.MapPost("/{id}/duplicate", async (string id, IWorkflowDefinitionStore store, CancellationToken ct) =>
        {
            var duplicate = await store.DuplicateAsync(id, ct);
            return duplicate is null ? Results.NotFound() : Results.Created($"/api/workflows/{duplicate.Id}", duplicate);
        }).WithName("DuplicateWorkflow");

        group.MapPost("/{id}/run", async (string id, WorkflowRunService runService, IAuditTrailService audit, HttpContext http, CancellationToken ct) =>
        {
            var run = await runService.StartRunAsync(id, ct);
            if (run is null) return Results.NotFound();
            audit.Log("run.started", id, $"Run started: {run.RunId}", ipAddress: http.Connection.RemoteIpAddress?.ToString());
            return Results.Ok(run);
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

    private static void MapTemplateEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/templates").WithTags("Templates");

        group.MapGet("/", async (string? category, string? tag, IWorkflowTemplateLibrary library, CancellationToken ct) =>
        {
            var templates = await library.GetTemplatesAsync(category, tag, ct);
            return Results.Ok(templates);
        }).WithName("ListTemplates");

        group.MapGet("/categories", async (IWorkflowTemplateLibrary library, CancellationToken ct) =>
        {
            var categories = await library.GetCategoriesAsync(ct);
            return Results.Ok(categories);
        }).WithName("ListTemplateCategories");

        group.MapGet("/{id}", async (string id, IWorkflowTemplateLibrary library, CancellationToken ct) =>
        {
            var template = await library.GetTemplateAsync(id, ct);
            return template is null ? Results.NotFound() : Results.Ok(template);
        }).WithName("GetTemplate");

        group.MapPost("/{id}/use", async (string id, IWorkflowTemplateLibrary library, IWorkflowDefinitionStore store, IWorkflowVersioningService versioning, IAuditTrailService audit, HttpContext http, CancellationToken ct) =>
        {
            var template = await library.GetTemplateAsync(id, ct);
            if (template is null)
                return Results.NotFound();

            var request = new CreateWorkflowRequest
            {
                Description = template.Description,
                Definition = template.Definition
            };
            var created = await store.CreateAsync(request, ct);
            versioning.CreateVersion(created, changeSummary: $"Created from template '{template.Name}'");
            audit.Log("template.used", created.Id, $"Created from template '{template.Name}' ({id})", ipAddress: http.Connection.RemoteIpAddress?.ToString());
            return Results.Created($"/api/workflows/{created.Id}", created);
        }).WithName("UseTemplate");
    }

    private static void MapVersionEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/workflows").WithTags("Versioning");

        group.MapGet("/{id}/versions", (string id, IWorkflowVersioningService versioning) =>
        {
            var versions = versioning.GetVersions(id);
            return Results.Ok(versions.Select(v => new
            {
                v.VersionNumber,
                v.Timestamp,
                v.Author,
                v.ChangeSummary
            }));
        }).WithName("ListWorkflowVersions");

        group.MapGet("/{id}/versions/{version:int}", (string id, int version, IWorkflowVersioningService versioning) =>
        {
            var v = versioning.GetVersion(id, version);
            return v is null ? Results.NotFound() : Results.Ok(v);
        }).WithName("GetWorkflowVersion");

        group.MapPost("/{id}/versions/{version:int}/restore", async (string id, int version, IWorkflowVersioningService versioning, IWorkflowDefinitionStore store, IAuditTrailService audit, HttpContext http, CancellationToken ct) =>
        {
            var v = versioning.GetVersion(id, version);
            if (v is null) return Results.NotFound();

            var request = new CreateWorkflowRequest
            {
                Description = v.Snapshot.Description,
                Tags = v.Snapshot.Tags,
                Definition = v.Snapshot.Definition
            };
            var updated = await store.UpdateAsync(id, request, ct);
            if (updated is null) return Results.NotFound();
            versioning.CreateVersion(updated, changeSummary: $"Restored from version {version}");
            audit.Log("version.restored", id, $"Restored workflow to version {version}", ipAddress: http.Connection.RemoteIpAddress?.ToString());
            return Results.Ok(updated);
        }).WithName("RestoreWorkflowVersion");

        group.MapGet("/{id}/diff", (string id, int from, int to, IWorkflowVersioningService versioning) =>
        {
            var diff = versioning.Diff(id, from, to);
            return diff is null ? Results.NotFound() : Results.Ok(diff);
        }).WithName("DiffWorkflowVersions");
    }

    private static void MapAuditEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/audit").WithTags("Audit");

        group.MapGet("/", (string? action, string? workflowId, string? userId, DateTimeOffset? from, DateTimeOffset? to, int? limit, IAuditTrailService audit) =>
        {
            var entries = audit.Query(action, workflowId, userId, from, to, limit ?? 100);
            return Results.Ok(entries);
        }).WithName("ListAuditEntries");

        group.MapGet("/workflow/{id}", (string id, int? limit, IAuditTrailService audit) =>
        {
            var entries = audit.GetForWorkflow(id, limit ?? 100);
            return Results.Ok(entries);
        }).WithName("GetWorkflowAudit");
    }

    private static void MapTagEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/tags", async (IWorkflowDefinitionStore store, CancellationToken ct) =>
        {
            var workflows = await store.GetAllAsync(ct);
            var tags = workflows.SelectMany(w => w.Tags).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t).ToList();
            return Results.Ok(tags);
        }).WithTags("Tags").WithName("ListTags");
    }

    private static void MapValidationEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Validate an existing saved workflow by ID
        endpoints.MapPost("/api/workflows/{id}/validate", async (string id, WorkflowValidator validator, IWorkflowDefinitionStore store, CancellationToken ct) =>
        {
            var workflow = await store.GetByIdAsync(id, ct);
            if (workflow is null) return Results.NotFound();
            var result = validator.Validate(workflow.Definition);
            return Results.Ok(result);
        }).WithTags("Validation").WithName("ValidateWorkflowById");

        // Validate a workflow definition without saving
        endpoints.MapPost("/api/workflows/validate", (WorkflowDefinitionDto definition, WorkflowValidator validator) =>
        {
            var result = validator.Validate(definition);
            return Results.Ok(result);
        }).WithTags("Validation").WithName("ValidateWorkflowDefinition");
    }

    private static void MapSettingsEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/settings").WithTags("Settings");

        group.MapGet("/", (IDashboardSettingsService svc) => Results.Ok(svc.Get()))
            .WithName("GetSettings");

        group.MapPut("/", (DashboardSettings settings, IDashboardSettingsService svc) =>
        {
            svc.Update(settings);
            return Results.Ok(svc.Get());
        }).WithName("UpdateSettings");

        group.MapPost("/test-ollama", async (IDashboardSettingsService svc, IHttpClientFactory factory) =>
        {
            var settings = svc.Get();
            try
            {
                var client = factory.CreateClient("OllamaClient");
                client.Timeout = TimeSpan.FromSeconds(5);
                var resp = await client.GetAsync($"{settings.OllamaUrl.TrimEnd('/')}/api/tags");
                return resp.IsSuccessStatusCode
                    ? Results.Ok(new { success = true, message = "Connected to Ollama" })
                    : Results.Ok(new { success = false, message = $"Ollama returned {resp.StatusCode}" });
            }
            catch (Exception ex)
            {
                return Results.Ok(new { success = false, message = ex.Message });
            }
        }).WithName("TestOllamaConnection");

        endpoints.MapGet("/api/providers/{provider}/models", async (string provider, IDashboardSettingsService svc, IHttpClientFactory factory) =>
        {
            var settings = svc.Get();
            switch (provider.ToLowerInvariant())
            {
                case "ollama":
                    try
                    {
                        var client = factory.CreateClient("OllamaClient");
                        client.Timeout = TimeSpan.FromSeconds(10);
                        var resp = await client.GetAsync($"{settings.OllamaUrl.TrimEnd('/')}/api/tags");
                        if (resp.IsSuccessStatusCode)
                        {
                            var json = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                            var models = new List<string>();
                            if (json.TryGetProperty("models", out var arr))
                            {
                                foreach (var m in arr.EnumerateArray())
                                {
                                    if (m.TryGetProperty("name", out var n))
                                        models.Add(n.GetString() ?? "");
                                }
                            }
                            return Results.Ok(models);
                        }
                    }
                    catch { }
                    return Results.Ok(Array.Empty<string>());

                case "openai":
                    return Results.Ok(new[] { "gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-4", "gpt-3.5-turbo", "o1-preview", "o1-mini" });

                case "anthropic":
                    return Results.Ok(new[] { "claude-opus-4-20250514", "claude-sonnet-4-20250514", "claude-3-5-haiku-20241022", "claude-3-opus-20240229" });

                case "huggingface":
                    return Results.Ok(new[] { "meta-llama/Llama-3-70b-chat-hf", "mistralai/Mixtral-8x7B-Instruct-v0.1", "microsoft/Phi-3-mini-4k-instruct" });

                default:
                    return Results.Ok(Array.Empty<string>());
            }
        }).WithTags("Settings").WithName("GetProviderModels");
    }
}
