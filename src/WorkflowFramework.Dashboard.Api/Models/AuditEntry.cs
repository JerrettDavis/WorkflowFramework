namespace WorkflowFramework.Dashboard.Api.Models;

/// <summary>
/// An audit log entry tracking a user action.
/// </summary>
public sealed class AuditEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string Action { get; set; } = "";
    public string UserId { get; set; } = "anonymous";
    public string? WorkflowId { get; set; }
    public string Details { get; set; } = "";
    public string? IpAddress { get; set; }
}
