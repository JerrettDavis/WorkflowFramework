using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Services;
using WorkflowFramework.Dashboard.Persistence;
using WorkflowFramework.Dashboard.Persistence.Entities;
using WorkflowFramework.Serialization;

namespace WorkflowFramework.Dashboard.Api.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IWorkflowDefinitionStore"/> with SQLite persistence.
/// </summary>
public sealed class EfWorkflowDefinitionStore : IWorkflowDefinitionStore
{
    private readonly DashboardDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Default owner ID used when no authentication is configured.</summary>
    public const string SystemUserId = "system";

    public EfWorkflowDefinitionStore(DashboardDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private string EffectiveUserId => _currentUser.UserId ?? SystemUserId;
    private bool IsScoped => _currentUser.IsAuthenticated && _currentUser.UserId != SystemUserId;

    public async Task<IReadOnlyList<SavedWorkflowDefinition>> GetAllAsync(CancellationToken ct = default)
    {
        IQueryable<WorkflowEntity> query = _db.Workflows;
        if (IsScoped)
            query = query.Where(w => w.OwnerId == EffectiveUserId);

        var entities = (await query.ToListAsync(ct))
            .OrderByDescending(w => w.LastModifiedAt)
            .ToList();
        return entities.Select(ToModel).ToList();
    }

    public async Task<SavedWorkflowDefinition?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var entity = await _db.Workflows.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (entity is null) return null;
        if (IsScoped && entity.OwnerId != EffectiveUserId) return null;
        return ToModel(entity);
    }

    public async Task<SavedWorkflowDefinition> CreateAsync(CreateWorkflowRequest request, CancellationToken ct = default)
    {
        var entity = new WorkflowEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = EffectiveUserId,
            Name = request.Definition.Name,
            Description = request.Description,
            DefinitionJson = JsonSerializer.Serialize(request.Definition, JsonOptions),
            TagsJson = JsonSerializer.Serialize(request.Tags, JsonOptions),
            CurrentVersion = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow
        };

        _db.Workflows.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToModel(entity);
    }

    public async Task<SavedWorkflowDefinition?> UpdateAsync(string id, CreateWorkflowRequest request, CancellationToken ct = default)
    {
        var entity = await _db.Workflows.FindAsync([id], ct);
        if (entity is null) return null;
        if (IsScoped && entity.OwnerId != EffectiveUserId) return null;

        entity.Name = request.Definition.Name;
        entity.Description = request.Description;
        entity.DefinitionJson = JsonSerializer.Serialize(request.Definition, JsonOptions);
        entity.TagsJson = JsonSerializer.Serialize(request.Tags, JsonOptions);
        entity.CurrentVersion++;
        entity.LastModifiedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToModel(entity);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        var entity = await _db.Workflows.FindAsync([id], ct);
        if (entity is null) return false;
        if (IsScoped && entity.OwnerId != EffectiveUserId) return false;

        entity.IsDeleted = true;
        entity.LastModifiedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SavedWorkflowDefinition?> DuplicateAsync(string id, CancellationToken ct = default)
    {
        var entity = await _db.Workflows.FindAsync([id], ct);
        if (entity is null) return null;

        var definition = JsonSerializer.Deserialize<WorkflowDefinitionDto>(entity.DefinitionJson, JsonOptions)
                         ?? new WorkflowDefinitionDto();
        definition.Name += " (Copy)";

        var duplicate = new WorkflowEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = entity.OwnerId,
            Name = definition.Name,
            Description = entity.Description,
            DefinitionJson = JsonSerializer.Serialize(definition, JsonOptions),
            TagsJson = entity.TagsJson,
            CurrentVersion = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = DateTimeOffset.UtcNow
        };

        _db.Workflows.Add(duplicate);
        await _db.SaveChangesAsync(ct);
        return ToModel(duplicate);
    }

    public async Task SeedAsync(SavedWorkflowDefinition workflow, CancellationToken ct = default)
    {
        var exists = await _db.Workflows
            .IgnoreQueryFilters()
            .AnyAsync(w => w.Id == workflow.Id, ct);
        if (exists) return;

        var entity = new WorkflowEntity
        {
            Id = workflow.Id,
            OwnerId = SystemUserId,
            Name = workflow.Definition.Name,
            Description = workflow.Description,
            DefinitionJson = JsonSerializer.Serialize(workflow.Definition, JsonOptions),
            TagsJson = JsonSerializer.Serialize(workflow.Tags, JsonOptions),
            CurrentVersion = 1,
            CreatedAt = DateTimeOffset.UtcNow,
            LastModifiedAt = workflow.LastModified
        };

        _db.Workflows.Add(entity);
        await _db.SaveChangesAsync(ct);
    }

    internal static SavedWorkflowDefinition ToModel(WorkflowEntity entity) => new()
    {
        Id = entity.Id,
        Description = entity.Description,
        Tags = JsonSerializer.Deserialize<List<string>>(entity.TagsJson, JsonOptions) ?? [],
        LastModified = entity.LastModifiedAt,
        Definition = JsonSerializer.Deserialize<WorkflowDefinitionDto>(entity.DefinitionJson, JsonOptions)
                     ?? new WorkflowDefinitionDto()
    };
}
