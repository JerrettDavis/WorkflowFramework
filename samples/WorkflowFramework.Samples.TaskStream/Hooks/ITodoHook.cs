using WorkflowFramework.Samples.TaskStream.Models;

namespace WorkflowFramework.Samples.TaskStream.Hooks;

/// <summary>
/// Hook interface for reacting to todo item lifecycle events.
/// </summary>
public interface ITodoHook
{
    /// <summary>Called when a task is created.</summary>
    Task OnTaskCreatedAsync(TodoItem item, CancellationToken ct = default);

    /// <summary>Called when a task is updated.</summary>
    Task OnTaskUpdatedAsync(TodoItem item, CancellationToken ct = default);

    /// <summary>Called when a task is completed.</summary>
    Task OnTaskCompletedAsync(TodoItem item, CancellationToken ct = default);
}
