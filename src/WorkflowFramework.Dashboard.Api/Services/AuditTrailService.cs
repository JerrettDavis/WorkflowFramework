using System.Collections.Concurrent;
using WorkflowFramework.Dashboard.Api.Models;

namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// In-memory audit trail service that logs all dashboard actions.
/// </summary>
public sealed class AuditTrailService : IAuditTrailService
{
    private readonly ConcurrentBag<AuditEntry> _entries = [];

    public void Log(string action, string? workflowId = null, string details = "", string userId = "anonymous", string? ipAddress = null)
    {
        _entries.Add(new AuditEntry
        {
            Action = action,
            WorkflowId = workflowId,
            Details = details,
            UserId = userId,
            IpAddress = ipAddress
        });
    }

    public IReadOnlyList<AuditEntry> Query(
        string? action = null,
        string? workflowId = null,
        string? userId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int limit = 100)
    {
        IEnumerable<AuditEntry> q = _entries;
        if (action is not null) q = q.Where(e => e.Action == action);
        if (workflowId is not null) q = q.Where(e => e.WorkflowId == workflowId);
        if (userId is not null) q = q.Where(e => e.UserId == userId);
        if (from.HasValue) q = q.Where(e => e.Timestamp >= from.Value);
        if (to.HasValue) q = q.Where(e => e.Timestamp <= to.Value);
        return q.OrderByDescending(e => e.Timestamp).Take(limit).ToList();
    }

    public IReadOnlyList<AuditEntry> GetForWorkflow(string workflowId, int limit = 100)
        => Query(workflowId: workflowId, limit: limit);
}
