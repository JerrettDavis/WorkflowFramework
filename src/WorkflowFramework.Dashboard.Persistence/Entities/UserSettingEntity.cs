namespace WorkflowFramework.Dashboard.Persistence.Entities;

/// <summary>
/// Per-user key-value setting stored in the database.
/// </summary>
public sealed class UserSettingEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    // Navigation
    public DashboardUser? User { get; set; }
}
