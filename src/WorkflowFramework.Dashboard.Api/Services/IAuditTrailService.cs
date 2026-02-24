using WorkflowFramework.Dashboard.Api.Models;

namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// Audit trail service for logging dashboard actions.
/// </summary>
public interface IAuditTrailService
{
    void Log(string action, string? workflowId = null, string details = "", string userId = "anonymous", string? ipAddress = null);

    IReadOnlyList<AuditEntry> Query(
        string? action = null,
        string? workflowId = null,
        string? userId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int limit = 100);

    IReadOnlyList<AuditEntry> GetForWorkflow(string workflowId, int limit = 100);
}
