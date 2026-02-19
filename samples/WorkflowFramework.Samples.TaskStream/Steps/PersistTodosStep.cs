using WorkflowFramework.Samples.TaskStream.Hooks;
using WorkflowFramework.Samples.TaskStream.Models;
using WorkflowFramework.Samples.TaskStream.Store;

namespace WorkflowFramework.Samples.TaskStream.Steps;

/// <summary>
/// Persists validated todos to the store and fires creation hooks.
/// </summary>
public sealed class PersistTodosStep : IStep
{
    private readonly ITodoStore _store;
    private readonly IEnumerable<ITodoHook> _hooks;

    /// <summary>Initializes a new instance.</summary>
    public PersistTodosStep(ITodoStore store, IEnumerable<ITodoHook> hooks)
    {
        _store = store;
        _hooks = hooks;
    }

    /// <inheritdoc />
    public string Name => "PersistTodos";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var todos = (List<TodoItem>)context.Properties["validatedTodos"]!;

        foreach (var item in todos)
        {
            await _store.AddAsync(item, context.CancellationToken);
            foreach (var hook in _hooks)
                await hook.OnTaskCreatedAsync(item, context.CancellationToken);
        }

        Console.WriteLine($"  💾 Persisted {todos.Count} tasks");
    }
}
