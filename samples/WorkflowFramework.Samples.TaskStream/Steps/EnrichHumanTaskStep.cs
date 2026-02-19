using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Samples.TaskStream.Hooks;
using WorkflowFramework.Samples.TaskStream.Models;
using WorkflowFramework.Samples.TaskStream.Store;

namespace WorkflowFramework.Samples.TaskStream.Steps;

/// <summary>
/// Enriches human-required tasks with suggestions, priority, and time estimates.
/// </summary>
public sealed class EnrichHumanTaskStep : IStep
{
    private readonly IAgentProvider _agent;
    private readonly ITodoStore _store;
    private readonly IEnumerable<ITodoHook> _hooks;

    /// <summary>Initializes a new instance.</summary>
    public EnrichHumanTaskStep(IAgentProvider agent, ITodoStore store, IEnumerable<ITodoHook> hooks)
    {
        _agent = agent;
        _store = store;
        _hooks = hooks;
    }

    /// <inheritdoc />
    public string Name => "EnrichHumanTasks";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var tasks = (List<TodoItem>)context.Properties["humanTasks"]!;

        foreach (var item in tasks)
        {
            var response = await _agent.CompleteAsync(new LlmRequest
            {
                Prompt = "Enrich this human task with suggestions.",
                Variables = new Dictionary<string, object?> { ["taskTitle"] = item.Title }
            }, context.CancellationToken);

            // Parse enrichment lines (key:value format)
            foreach (var line in response.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx > 0)
                {
                    var key = line[..colonIdx].Trim();
                    var value = line[(colonIdx + 1)..].Trim();
                    item.Enrichments[key] = value;

                    if (key == "priority" && int.TryParse(value, out var p))
                        item.Priority = p;
                }
            }

            item.Status = TodoStatus.Pending;
            await _store.UpdateAsync(item, context.CancellationToken);
            foreach (var hook in _hooks)
                await hook.OnTaskUpdatedAsync(item, context.CancellationToken);
        }

        context.Properties["enrichedTasks"] = tasks;
        Console.WriteLine($"  💡 Enriched {tasks.Count} human tasks");
    }
}
