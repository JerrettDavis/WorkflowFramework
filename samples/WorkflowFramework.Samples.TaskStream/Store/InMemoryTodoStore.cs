using System.Collections.Concurrent;
using WorkflowFramework.Samples.TaskStream.Models;

namespace WorkflowFramework.Samples.TaskStream.Store;

/// <summary>
/// In-memory implementation of <see cref="ITodoStore"/> using a concurrent dictionary.
/// </summary>
public sealed class InMemoryTodoStore : ITodoStore
{
    private readonly ConcurrentDictionary<string, TodoItem> _items = new();

    /// <inheritdoc />
    public Task AddAsync(TodoItem item, CancellationToken ct = default)
    {
        _items[item.Id] = item;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(TodoItem item, CancellationToken ct = default)
    {
        _items[item.Id] = item;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<TodoItem?> GetAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_items.TryGetValue(id, out var item) ? item : null);

    /// <inheritdoc />
    public Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TodoItem>>([.. _items.Values]);

    /// <inheritdoc />
    public Task DeleteAsync(string id, CancellationToken ct = default)
    {
        _items.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TodoItem>> SearchAsync(string query, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TodoItem>>(
            [.. _items.Values.Where(i => i.Title.Contains(query, StringComparison.OrdinalIgnoreCase))]);
}
