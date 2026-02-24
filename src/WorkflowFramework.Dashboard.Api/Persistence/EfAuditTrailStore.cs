using Microsoft.EntityFrameworkCore;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Services;
using WorkflowFramework.Dashboard.Persistence;
using WorkflowFramework.Dashboard.Persistence.Entities;

namespace WorkflowFramework.Dashboard.Api.Persistence;

/// <summary>
/// EF Core audit trail service replacing the in-memory ConcurrentBag implementation.
/// </summary>
public sealed class EfAuditTrailStore : IAuditTrailService
{
    private readonly DashboardDbContext _db;

    public EfAuditTrailStore(DashboardDbContext db) => _db = db;

    public void Log(string action, string? workflowId = null, string details = "",
        string userId = "anonymous", string? ipAddress = null)
    {
        _db.AuditEntries.Add(new AuditEntryEntity
        {
            Action = action,
            WorkflowId = workflowId,
            UserId = userId,
            Details = details,
            IpAddress = ipAddress,
            Timestamp = DateTimeOffset.UtcNow
        });
        _db.SaveChanges();
    }

    public async Task LogAsync(string action, string? workflowId = null, string details = "",
        string userId = "anonymous", string? ipAddress = null, CancellationToken ct = default)
    {
        _db.AuditEntries.Add(new AuditEntryEntity
        {
            Action = action,
            WorkflowId = workflowId,
            UserId = userId,
            Details = details,
            IpAddress = ipAddress,
            Timestamp = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public IReadOnlyList<AuditEntry> Query(
        string? action = null, string? workflowId = null, string? userId = null,
        DateTimeOffset? from = null, DateTimeOffset? to = null, int limit = 100)
    {
        IQueryable<AuditEntryEntity> q = _db.AuditEntries;
        if (action is not null) q = q.Where(e => e.Action == action);
        if (workflowId is not null) q = q.Where(e => e.WorkflowId == workflowId);
        if (userId is not null) q = q.Where(e => e.UserId == userId);
        if (from.HasValue) q = q.Where(e => e.Timestamp >= from.Value);
        if (to.HasValue) q = q.Where(e => e.Timestamp <= to.Value);

        return q.AsEnumerable().OrderByDescending(e => e.Timestamp).Take(limit).Select(ToModel).ToList();
    }

    public IReadOnlyList<AuditEntry> GetForWorkflow(string workflowId, int limit = 100)
        => Query(workflowId: workflowId, limit: limit);

    private static AuditEntry ToModel(AuditEntryEntity e) => new()
    {
        Id = e.Id,
        Action = e.Action,
        WorkflowId = e.WorkflowId,
        UserId = e.UserId ?? "anonymous",
        Details = e.Details ?? "",
        IpAddress = e.IpAddress,
        Timestamp = e.Timestamp
    };
}
