using WorkflowFramework.Samples.TaskStream.Models;

namespace WorkflowFramework.Samples.TaskStream.Steps;

/// <summary>
/// Combines automated and enriched results into a <see cref="TaskStreamResult"/>.
/// </summary>
public sealed class AggregateResultsStep : IStep
{
    /// <inheritdoc />
    public string Name => "AggregateResults";

    /// <inheritdoc />
    public Task ExecuteAsync(IWorkflowContext context)
    {
        var automatedResults = context.Properties.TryGetValue("automatedResults", out var ar) ? (List<AutomatedResult>)ar! : [];
        var enrichedTasks = context.Properties.TryGetValue("enrichedTasks", out var et) ? (List<TodoItem>)et! : [];
        var sourceMessages = context.Properties.TryGetValue("sourceMessages", out var sm) ? (List<SourceMessage>)sm! : [];
        var validatedTodos = context.Properties.TryGetValue("validatedTodos", out var vt) ? (List<TodoItem>)vt! : [];
        var duplicatesRemoved = context.Properties.TryGetValue("duplicatesRemoved", out var d) ? (int)d! : 0;

        var result = new TaskStreamResult
        {
            ProcessedItems = [.. automatedResults.Select(r => r.Task), .. enrichedTasks],
            AutomatedResults = automatedResults,
            EnrichedItems = enrichedTasks,
            Stats = new PipelineStats
            {
                TotalMessages = sourceMessages.Count,
                TotalTasks = validatedTodos.Count,
                AutomatedCount = automatedResults.Count,
                HumanCount = enrichedTasks.Count,
                DuplicatesRemoved = duplicatesRemoved
            }
        };

        context.Properties["taskStreamResult"] = result;
        Console.WriteLine($"  📊 Aggregated results: {result.ProcessedItems.Count} total tasks");
        return Task.CompletedTask;
    }
}
