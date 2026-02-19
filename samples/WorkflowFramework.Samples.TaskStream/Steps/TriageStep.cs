using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Samples.TaskStream.Models;

namespace WorkflowFramework.Samples.TaskStream.Steps;

/// <summary>
/// Uses the AI agent to classify each task as automatable or human-required.
/// </summary>
public sealed class TriageStep : IStep
{
    private readonly IAgentProvider _agent;

    /// <summary>Initializes a new instance.</summary>
    public TriageStep(IAgentProvider agent) => _agent = agent;

    /// <inheritdoc />
    public string Name => "Triage";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var todos = (List<TodoItem>)context.Properties["validatedTodos"]!;
        var automatable = new List<TodoItem>();
        var human = new List<TodoItem>();

        foreach (var item in todos)
        {
            var decision = await _agent.DecideAsync(new AgentDecisionRequest
            {
                Prompt = "Triage: classify this task",
                Options = ["Automatable", "HumanRequired", "Hybrid"],
                Variables = new Dictionary<string, object?> { ["taskTitle"] = item.Title }
            }, context.CancellationToken);

            if (Enum.TryParse<TaskCategory>(decision, out var cat))
                item.Category = cat;

            if (item.Category == TaskCategory.Automatable)
                automatable.Add(item);
            else
                human.Add(item);
        }

        context.Properties["automatableTasks"] = automatable;
        context.Properties["humanTasks"] = human;
        Console.WriteLine($"  🏷️  Triaged: {automatable.Count} automatable, {human.Count} human-required");
    }
}
