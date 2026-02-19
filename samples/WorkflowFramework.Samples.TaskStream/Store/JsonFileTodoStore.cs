using System.Text.Json;
using WorkflowFramework.Samples.TaskStream.Models;

namespace WorkflowFramework.Samples.TaskStream.Store;

/// <summary>
/// File-backed implementation of <see cref="ITodoStore"/> using JSON serialization.
/// Thread-safe via <see cref="SemaphoreSlim"/>.
/// </summary>
public sealed class JsonFileTodoStore : ITodoStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Initializes a new instance writing to the given file path.</summary>
    public JsonFileTodoStore(string filePath)
    {
        _filePath = filePath;
    }

    private async Task<Dictionary<string, TodoItem>> LoadAsync()
    {
        if (!File.Exists(_filePath))
            return [];

        var json = await File.ReadAllTextAsync(_filePath);
        return JsonSerializer.Deserialize<Dictionary<string, TodoItem>>(json, JsonOptions) ?? [];
    }

    private async Task SaveAsync(Dictionary<string, TodoItem> items)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(items, JsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }

    /// <inheritdoc />
    public async Task AddAsync(TodoItem item, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var items = await LoadAsync();
            items[item.Id] = item;
            await SaveAsync(items);
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc />
    public async Task UpdateAsync(TodoItem item, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var items = await LoadAsync();
            items[item.Id] = item;
            await SaveAsync(items);
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc />
    public async Task<TodoItem?> GetAsync(string id, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var items = await LoadAsync();
            return items.GetValueOrDefault(id);
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var items = await LoadAsync();
            return [.. items.Values];
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var items = await LoadAsync();
            items.Remove(id);
            await SaveAsync(items);
        }
        finally { _lock.Release(); }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TodoItem>> SearchAsync(string query, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var items = await LoadAsync();
            return [.. items.Values.Where(i => i.Title.Contains(query, StringComparison.OrdinalIgnoreCase))];
        }
        finally { _lock.Release(); }
    }
}
