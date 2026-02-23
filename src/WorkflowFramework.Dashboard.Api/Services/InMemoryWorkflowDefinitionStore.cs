using System.Collections.Concurrent;
using System.Text.Json;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Serialization;

namespace WorkflowFramework.Dashboard.Api.Services;

/// <summary>
/// In-memory implementation of <see cref="IWorkflowDefinitionStore"/>.
/// </summary>
public sealed class InMemoryWorkflowDefinitionStore : IWorkflowDefinitionStore
{
    private readonly ConcurrentDictionary<string, SavedWorkflowDefinition> _store = new();

    public Task<IReadOnlyList<SavedWorkflowDefinition>> GetAllAsync(CancellationToken ct = default)
    {
        var list = _store.Values.OrderByDescending(w => w.LastModified).ToList();
        return Task.FromResult<IReadOnlyList<SavedWorkflowDefinition>>(list);
    }

    public Task<SavedWorkflowDefinition?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var result);
        return Task.FromResult(result);
    }

    public Task<SavedWorkflowDefinition> CreateAsync(CreateWorkflowRequest request, CancellationToken ct = default)
    {
        var saved = new SavedWorkflowDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Description = request.Description,
            Tags = request.Tags,
            LastModified = DateTimeOffset.UtcNow,
            Definition = request.Definition
        };
        _store[saved.Id] = saved;
        return Task.FromResult(saved);
    }

    public Task<SavedWorkflowDefinition?> UpdateAsync(string id, CreateWorkflowRequest request, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(id, out var existing))
            return Task.FromResult<SavedWorkflowDefinition?>(null);

        var updated = new SavedWorkflowDefinition
        {
            Id = id,
            Description = request.Description,
            Tags = request.Tags,
            LastModified = DateTimeOffset.UtcNow,
            Definition = request.Definition
        };
        _store[id] = updated;
        return Task.FromResult<SavedWorkflowDefinition?>(updated);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        return Task.FromResult(_store.TryRemove(id, out _));
    }

    public Task<SavedWorkflowDefinition?> DuplicateAsync(string id, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(id, out var existing))
            return Task.FromResult<SavedWorkflowDefinition?>(null);

        // Deep clone the definition via JSON round-trip
        var json = JsonSerializer.Serialize(existing.Definition);
        var clonedDef = JsonSerializer.Deserialize<WorkflowDefinitionDto>(json)!;
        clonedDef.Name = existing.Definition.Name + " (Copy)";

        var duplicate = new SavedWorkflowDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Description = existing.Description,
            Tags = new List<string>(existing.Tags),
            LastModified = DateTimeOffset.UtcNow,
            Definition = clonedDef
        };
        _store[duplicate.Id] = duplicate;
        return Task.FromResult<SavedWorkflowDefinition?>(duplicate);
    }

    public Task SeedAsync(SavedWorkflowDefinition workflow, CancellationToken ct = default)
    {
        _store.TryAdd(workflow.Id, workflow);
        return Task.CompletedTask;
    }
}
