using WorkflowFramework.Samples.TaskStream.Models;

namespace WorkflowFramework.Samples.TaskStream.Store;

/// <summary>
/// Persistence store for todo items.
/// </summary>
public interface ITodoStore
{
    /// <summary>Adds a todo item.</summary>
    Task AddAsync(TodoItem item, CancellationToken ct = default);

    /// <summary>Updates an existing todo item.</summary>
    Task UpdateAsync(TodoItem item, CancellationToken ct = default);

    /// <summary>Gets a todo item by id.</summary>
    Task<TodoItem?> GetAsync(string id, CancellationToken ct = default);

    /// <summary>Gets all todo items.</summary>
    Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Deletes a todo item by id.</summary>
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>Searches todo items by title substring.</summary>
    Task<IReadOnlyList<TodoItem>> SearchAsync(string query, CancellationToken ct = default);
}
