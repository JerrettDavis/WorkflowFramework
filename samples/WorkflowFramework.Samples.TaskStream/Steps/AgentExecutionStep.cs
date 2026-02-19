using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Samples.TaskStream.Hooks;
using WorkflowFramework.Samples.TaskStream.Models;
using WorkflowFramework.Samples.TaskStream.Store;

namespace WorkflowFramework.Samples.TaskStream.Steps;

/// <summary>
/// Executes automatable tasks via the AI agent and marks them complete.
/// </summary>
public sealed class AgentExecutionStep : IStep
{
    private readonly IAgentProvider _agent;
    private readonly ITodoStore _store;
    private readonly IEnumerable<ITodoHook> _hooks;

    /// <summary>Initializes a new instance.</summary>
    public AgentExecutionStep(IAgentProvider agent, ITodoStore store, IEnumerable<ITodoHook> hooks)
    {
        _agent = agent;
        _store = store;
        _hooks = hooks;
    }

    /// <inheritdoc />
    public string Name => "AgentExecution";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var tasks = (List<TodoItem>)context.Properties["automatableTasks"]!;
        var results = new List<AutomatedResult>();

        foreach (var item in tasks)
        {
            var response = await _agent.CompleteAsync(new LlmRequest
            {
                Prompt = "Execute/automate this task.",
                Variables = new Dictionary<string, object?> { ["taskTitle"] = item.Title }
            }, context.CancellationToken);

            item.Status = TodoStatus.Completed;
            item.CompletedAt = DateTimeOffset.UtcNow;
            await _store.UpdateAsync(item, context.CancellationToken);

            foreach (var hook in _hooks)
                await hook.OnTaskCompletedAsync(item, context.CancellationToken);

            results.Add(new AutomatedResult { Task = item, Result = response.Content });
        }

        context.Properties["automatedResults"] = results;
        Console.WriteLine($"  🤖 Automated {results.Count} tasks");
    }
}
