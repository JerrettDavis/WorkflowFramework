using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Samples.TaskStream.Models;

namespace WorkflowFramework.Samples.TaskStream.Steps;

/// <summary>
/// Uses the AI agent provider to extract individual todo items from normalized text.
/// </summary>
public sealed class ExtractTodosStep : IStep
{
    private readonly IAgentProvider _agent;

    /// <summary>Initializes a new instance.</summary>
    public ExtractTodosStep(IAgentProvider agent) => _agent = agent;

    /// <inheritdoc />
    public string Name => "ExtractTodos";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var normalized = (List<string>)context.Properties["normalizedMessages"]!;
        var messages = (List<SourceMessage>)context.Properties["sourceMessages"]!;
        var todos = new List<TodoItem>();

        for (var i = 0; i < normalized.Count; i++)
        {
            var request = new LlmRequest
            {
                Prompt = "Extract individual tasks from this text.",
                Variables = new Dictionary<string, object?> { ["content"] = normalized[i] }
            };

            var response = await _agent.CompleteAsync(request, context.CancellationToken);
            var taskTitles = response.Content
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var title in taskTitles)
            {
                todos.Add(new TodoItem
                {
                    Source = messages[i].Source,
                    SourceId = messages[i].Id,
                    Title = title,
                    Description = $"Extracted from: {normalized[i][..Math.Min(80, normalized[i].Length)]}..."
                });
            }
        }

        context.Properties["extractedTodos"] = todos;
        Console.WriteLine($"  🔍 Extracted {todos.Count} tasks");
    }
}
