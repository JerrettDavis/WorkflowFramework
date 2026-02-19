using System.Collections.Concurrent;
using WorkflowFramework.Extensions.HumanTasks;

namespace WorkflowFramework.Samples.VoiceWorkflows;

/// <summary>
/// ITaskInbox that auto-approves after printing the content (demo purposes).
/// </summary>
public sealed class SimulatedHumanTaskInbox : ITaskInbox
{
    private readonly ConcurrentDictionary<string, HumanTask> _tasks = new();

    public Task<HumanTask> CreateTaskAsync(HumanTask task, CancellationToken cancellationToken = default)
    {
        _tasks[task.Id] = task;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  📋 Human Review Required: {task.Title}");
        Console.ResetColor();
        if (!string.IsNullOrWhiteSpace(task.Description))
        {
            var preview = task.Description.Length > 200
                ? task.Description[..200] + "..."
                : task.Description;
            Console.WriteLine($"     {preview}");
        }
        return Task.FromResult(task);
    }

    public Task<HumanTask?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        _tasks.TryGetValue(taskId, out var task);
        return Task.FromResult(task);
    }

    public Task<IReadOnlyList<HumanTask>> GetTasksForAssigneeAsync(string assignee, CancellationToken cancellationToken = default)
    {
        var tasks = _tasks.Values.Where(t => t.Assignee == assignee).ToList();
        return Task.FromResult<IReadOnlyList<HumanTask>>(tasks);
    }

    public Task CompleteTaskAsync(string taskId, string outcome, IDictionary<string, object?>? data = null, CancellationToken cancellationToken = default)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            task.Status = HumanTaskStatus.Approved;
            task.Outcome = outcome;
        }
        return Task.CompletedTask;
    }

    public Task DelegateTaskAsync(string taskId, string newAssignee, CancellationToken cancellationToken = default)
    {
        if (_tasks.TryGetValue(taskId, out var task))
            task.Assignee = newAssignee;
        return Task.CompletedTask;
    }

    public Task<HumanTask> WaitForCompletionAsync(string taskId, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            // Auto-approve for demo
            task.Status = HumanTaskStatus.Approved;
            task.Outcome = "approved";
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  ✅ Auto-approved (simulated)");
            Console.ResetColor();
            return Task.FromResult(task);
        }
        throw new InvalidOperationException($"Task {taskId} not found");
    }
}
