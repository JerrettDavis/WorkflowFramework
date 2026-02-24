using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Persistence;
using WorkflowFramework.Dashboard.Persistence.Entities;

namespace WorkflowFramework.Dashboard.Api.Persistence;

/// <summary>
/// EF Core store for persisting completed workflow run history.
/// Active runs remain in-memory (via WorkflowRunService); this stores the final results.
/// </summary>
public sealed class EfWorkflowRunStore
{
    private readonly DashboardDbContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public EfWorkflowRunStore(DashboardDbContext db) => _db = db;

    public async Task SaveRunAsync(RunSummary run, CancellationToken ct = default)
    {
        var existing = await _db.WorkflowRuns.FindAsync([run.RunId], ct);
        if (existing is not null)
        {
            existing.Status = run.Status;
            existing.Error = run.Error;
            existing.CompletedAt = run.CompletedAt;
            existing.StepResultsJson = run.StepResults is not null
                ? JsonSerializer.Serialize(run.StepResults, JsonOptions) : null;
            if (run.CompletedAt.HasValue)
                existing.DurationMs = (long)(run.CompletedAt.Value - existing.StartedAt).TotalMilliseconds;
        }
        else
        {
            _db.WorkflowRuns.Add(new WorkflowRunEntity
            {
                Id = run.RunId,
                WorkflowId = run.WorkflowId,
                WorkflowName = run.WorkflowName,
                Status = run.Status,
                Error = run.Error,
                StartedAt = run.StartedAt,
                CompletedAt = run.CompletedAt,
                StepResultsJson = run.StepResults is not null
                    ? JsonSerializer.Serialize(run.StepResults, JsonOptions) : null,
                DurationMs = run.CompletedAt.HasValue
                    ? (long)(run.CompletedAt.Value - run.StartedAt).TotalMilliseconds : null
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<RunSummary>> GetRunsAsync(int limit = 100, CancellationToken ct = default)
    {
        var entities = (await _db.WorkflowRuns.ToListAsync(ct))
            .OrderByDescending(r => r.StartedAt)
            .Take(limit)
            .ToList();
        return entities.Select(ToModel).ToList();
    }

    public async Task<RunSummary?> GetRunAsync(string runId, CancellationToken ct = default)
    {
        var entity = await _db.WorkflowRuns.FindAsync([runId], ct);
        return entity is null ? null : ToModel(entity);
    }

    private static RunSummary ToModel(WorkflowRunEntity e) => new()
    {
        RunId = e.Id,
        WorkflowId = e.WorkflowId,
        WorkflowName = e.WorkflowName,
        Status = e.Status,
        StartedAt = e.StartedAt,
        CompletedAt = e.CompletedAt,
        Error = e.Error,
        StepResults = e.StepResultsJson is not null
            ? JsonSerializer.Deserialize<Dictionary<string, string>>(e.StepResultsJson, JsonOptions) : null
    };
}
