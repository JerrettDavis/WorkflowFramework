using WorkflowFramework.Samples.TaskStream.Models;

namespace WorkflowFramework.Samples.TaskStream.Sources;

/// <summary>
/// Merges messages from multiple task sources into a single stream.
/// </summary>
public sealed class AggregateTaskSource : ITaskSource
{
    private readonly IEnumerable<ITaskSource> _sources;

    /// <summary>Initializes a new instance with the given sources.</summary>
    public AggregateTaskSource(IEnumerable<ITaskSource> sources)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
    }

    /// <inheritdoc />
    public string Name => "Aggregate";

    /// <inheritdoc />
    public async IAsyncEnumerable<SourceMessage> GetMessagesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var source in _sources)
        {
            await foreach (var message in source.GetMessagesAsync(cancellationToken))
            {
                yield return message;
            }
        }
    }
}
