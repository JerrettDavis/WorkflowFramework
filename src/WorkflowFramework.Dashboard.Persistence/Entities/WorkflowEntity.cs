namespace WorkflowFramework.Dashboard.Persistence.Entities;

/// <summary>
/// Persisted workflow definition with ownership and soft-delete support.
/// </summary>
public sealed class WorkflowEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string OwnerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DefinitionJson { get; set; } = "{}";
    public string TagsJson { get; set; } = "[]";
    public int CurrentVersion { get; set; } = 1;
    public bool IsTemplate { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastModifiedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation
    public DashboardUser? Owner { get; set; }
    public List<WorkflowVersionEntity> Versions { get; set; } = [];
    public List<WorkflowRunEntity> Runs { get; set; } = [];
}
