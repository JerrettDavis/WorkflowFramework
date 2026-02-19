using WorkflowFramework.Samples.TaskStream.Models;
using WorkflowFramework.Samples.TaskStream.Sources;

namespace WorkflowFramework.Samples.TaskStream.Steps;

/// <summary>
/// Reads messages from the aggregate task source and stores them in context.
/// </summary>
public sealed class CollectSourcesStep : IStep
{
    private readonly AggregateTaskSource _source;

    /// <summary>Initializes a new instance.</summary>
    public CollectSourcesStep(AggregateTaskSource source) => _source = source;

    /// <inheritdoc />
    public string Name => "CollectSources";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var messages = new List<SourceMessage>();
        await foreach (var msg in _source.GetMessagesAsync(context.CancellationToken))
        {
            messages.Add(msg);
        }

        context.Properties["sourceMessages"] = messages;
        Console.WriteLine($"  📥 Collected {messages.Count} messages from sources");
    }
}
