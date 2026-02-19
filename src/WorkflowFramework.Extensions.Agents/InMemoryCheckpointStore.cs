using System.Collections.Concurrent;

namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// In-memory checkpoint store using ConcurrentDictionary.
/// </summary>
public sealed class InMemoryCheckpointStore : ICheckpointStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, (ContextSnapshot Snapshot, CheckpointInfo Info)>> _store = new();

    /// <inheritdoc />
    public Task SaveAsync(string workflowId, string checkpointId, ContextSnapshot snapshot, CancellationToken ct = default)
    {
        if (workflowId == null) throw new ArgumentNullException(nameof(workflowId));
        if (checkpointId == null) throw new ArgumentNullException(nameof(checkpointId));
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

        var wfStore = _store.GetOrAdd(workflowId, _ => new ConcurrentDictionary<string, (ContextSnapshot, CheckpointInfo)>());
        var info = new CheckpointInfo
        {
            Id = checkpointId,
            WorkflowId = workflowId,
            CreatedAt = DateTimeOffset.UtcNow,
            StepName = snapshot.StepName,
            MessageCount = snapshot.Messages.Count,
            EstimatedTokens = snapshot.Messages.Sum(m => (m.Content.Length + 3) / 4)
        };
        wfStore[checkpointId] = (snapshot, info);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ContextSnapshot?> LoadAsync(string workflowId, string checkpointId, CancellationToken ct = default)
    {
        if (_store.TryGetValue(workflowId, out var wfStore) && wfStore.TryGetValue(checkpointId, out var entry))
        {
            return Task.FromResult<ContextSnapshot?>(entry.Snapshot);
        }
        return Task.FromResult<ContextSnapshot?>(null);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CheckpointInfo>> ListAsync(string workflowId, CancellationToken ct = default)
    {
        if (_store.TryGetValue(workflowId, out var wfStore))
        {
            var list = wfStore.Values.Select(v => v.Info).OrderBy(i => i.CreatedAt).ToList();
            return Task.FromResult<IReadOnlyList<CheckpointInfo>>(list);
        }
        return Task.FromResult<IReadOnlyList<CheckpointInfo>>(Array.Empty<CheckpointInfo>());
    }

    /// <inheritdoc />
    public Task DeleteAsync(string workflowId, string checkpointId, CancellationToken ct = default)
    {
        if (_store.TryGetValue(workflowId, out var wfStore))
        {
            wfStore.TryRemove(checkpointId, out _);
        }
        return Task.CompletedTask;
    }
}
