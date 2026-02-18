using System.Collections.Concurrent;

namespace WorkflowFramework.Extensions.HumanTasks;

/// <summary>
/// In-memory implementation of <see cref="ITaskInbox"/>.
/// </summary>
public sealed class InMemoryTaskInbox : ITaskInbox
{
    private readonly ConcurrentDictionary<string, HumanTask> _tasks = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<HumanTask>> _waiters = new();

    /// <inheritdoc />
    public Task<HumanTask> CreateTaskAsync(HumanTask task, CancellationToken cancellationToken = default)
    {
        if (task == null) throw new ArgumentNullException(nameof(task));
        _tasks[task.Id] = task;
        return Task.FromResult(task);
    }

    /// <inheritdoc />
    public Task<HumanTask?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        _tasks.TryGetValue(taskId, out var task);
        return Task.FromResult(task);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<HumanTask>> GetTasksForAssigneeAsync(string assignee, CancellationToken cancellationToken = default)
    {
        var tasks = _tasks.Values.Where(t => t.Assignee == assignee).ToList();
        return Task.FromResult<IReadOnlyList<HumanTask>>(tasks);
    }

    /// <inheritdoc />
    public Task CompleteTaskAsync(string taskId, string outcome, IDictionary<string, object?>? data = null, CancellationToken cancellationToken = default)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.Status = outcome.Equals("approved", StringComparison.OrdinalIgnoreCase) ? HumanTaskStatus.Approved
                : outcome.Equals("rejected", StringComparison.OrdinalIgnoreCase) ? HumanTaskStatus.Rejected
                : HumanTaskStatus.Completed;
            task.Outcome = outcome;
            if (data != null)
            {
                foreach (var kv in data)
                    task.Data[kv.Key] = kv.Value;
            }
            if (_waiters.TryRemove(taskId, out var tcs))
                tcs.TrySetResult(task);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DelegateTaskAsync(string taskId, string newAssignee, CancellationToken cancellationToken = default)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.DelegatedTo = newAssignee;
            task.Assignee = newAssignee;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<HumanTask> WaitForCompletionAsync(string taskId, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        // Check if already complete
        if (_tasks.TryGetValue(taskId, out var task) && task.Status != HumanTaskStatus.Pending && task.Status != HumanTaskStatus.InProgress)
            return task;

        var tcs = new TaskCompletionSource<HumanTask>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiters[taskId] = tcs;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        using (cts.Token.Register(() => tcs.TrySetCanceled()))
        {
            return await tcs.Task.ConfigureAwait(false);
        }
    }
}
