using WorkflowFramework.Samples.TaskStream.Models;

namespace WorkflowFramework.Samples.TaskStream.Hooks;

/// <summary>
/// Logs todo item events to the console.
/// </summary>
public sealed class ConsoleHook : ITodoHook
{
    /// <inheritdoc />
    public Task OnTaskCreatedAsync(TodoItem item, CancellationToken ct = default)
    {
        Console.WriteLine($"    📋 Created: {item.Title} [{item.Category}]");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnTaskUpdatedAsync(TodoItem item, CancellationToken ct = default)
    {
        Console.WriteLine($"    ✏️  Updated: {item.Title} → {item.Status}");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnTaskCompletedAsync(TodoItem item, CancellationToken ct = default)
    {
        Console.WriteLine($"    ✅ Completed: {item.Title}");
        return Task.CompletedTask;
    }
}
