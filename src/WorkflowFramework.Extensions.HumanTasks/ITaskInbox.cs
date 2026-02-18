namespace WorkflowFramework.Extensions.HumanTasks;

/// <summary>
/// Abstraction for managing human tasks.
/// </summary>
public interface ITaskInbox
{
    /// <summary>Creates a new task.</summary>
    Task<HumanTask> CreateTaskAsync(HumanTask task, CancellationToken cancellationToken = default);

    /// <summary>Gets a task by ID.</summary>
    Task<HumanTask?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>Gets all tasks for an assignee.</summary>
    Task<IReadOnlyList<HumanTask>> GetTasksForAssigneeAsync(string assignee, CancellationToken cancellationToken = default);

    /// <summary>Completes a task with the given outcome.</summary>
    Task CompleteTaskAsync(string taskId, string outcome, IDictionary<string, object?>? data = null, CancellationToken cancellationToken = default);

    /// <summary>Delegates a task to another assignee.</summary>
    Task DelegateTaskAsync(string taskId, string newAssignee, CancellationToken cancellationToken = default);

    /// <summary>Waits for a task to be completed.</summary>
    Task<HumanTask> WaitForCompletionAsync(string taskId, TimeSpan timeout, CancellationToken cancellationToken = default);
}
