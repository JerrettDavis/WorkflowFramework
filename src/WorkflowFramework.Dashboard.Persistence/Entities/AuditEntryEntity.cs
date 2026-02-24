namespace WorkflowFramework.Dashboard.Persistence.Entities;

/// <summary>
/// Persisted audit trail entry for dashboard actions.
/// </summary>
public sealed class AuditEntryEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Action { get; set; } = string.Empty;
    public string? WorkflowId { get; set; }
    public string? UserId { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
