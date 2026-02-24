namespace WorkflowFramework.Dashboard.Persistence.Entities;

/// <summary>
/// Represents a dashboard user for multi-user workflow ownership.
/// </summary>
public sealed class DashboardUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastLoginAt { get; set; } = DateTimeOffset.UtcNow;

    // Auth
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTimeOffset? RefreshTokenExpiry { get; set; }

    // Navigation
    public List<WorkflowEntity> Workflows { get; set; } = [];
    public List<WorkflowRunEntity> Runs { get; set; } = [];
    public List<UserSettingEntity> Settings { get; set; } = [];
    public List<ApiKeyEntity> ApiKeys { get; set; } = [];
}
