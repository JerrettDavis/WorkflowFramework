using WorkflowFramework.Samples.TaskStream.Models;

namespace WorkflowFramework.Samples.TaskStream.Steps;

/// <summary>
/// Validates required fields and removes duplicate tasks by content hash.
/// </summary>
public sealed class ValidateAndDeduplicateStep : IStep
{
    /// <inheritdoc />
    public string Name => "ValidateAndDeduplicate";

    /// <inheritdoc />
    public Task ExecuteAsync(IWorkflowContext context)
    {
        var todos = (List<TodoItem>)context.Properties["extractedTodos"]!;
        var seen = new HashSet<string>();
        var validated = new List<TodoItem>();
        var duplicates = 0;

        foreach (var item in todos)
        {
            if (string.IsNullOrWhiteSpace(item.Title))
                continue;

            if (!seen.Add(item.ContentHash))
            {
                duplicates++;
                continue;
            }

            validated.Add(item);
        }

        context.Properties["validatedTodos"] = validated;
        context.Properties["duplicatesRemoved"] = duplicates;
        Console.WriteLine($"  ✅ Validated {validated.Count} tasks ({duplicates} duplicates removed)");
        return Task.CompletedTask;
    }
}
