namespace WorkflowFramework.Dashboard.Persistence.Entities;

/// <summary>
/// API key for external/programmatic access to the dashboard.
/// </summary>
public sealed class ApiKeyEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = "";
    public string Name { get; set; } = "";
    public string KeyHash { get; set; } = "";
    public string KeyPrefix { get; set; } = "";
    public string ScopesJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public bool IsRevoked { get; set; }

    // Navigation
    public DashboardUser? User { get; set; }
}
