using WorkflowFramework.Samples.TaskStream.Models;

namespace WorkflowFramework.Samples.TaskStream.Sources;

/// <summary>
/// A task source backed by an in-memory list. Useful for testing and demos.
/// </summary>
public sealed class InMemoryTaskSource : ITaskSource
{
    private readonly List<SourceMessage> _messages;

    /// <summary>Initializes a new instance with the given messages.</summary>
    public InMemoryTaskSource(IEnumerable<SourceMessage> messages)
    {
        _messages = [.. messages];
    }

    /// <inheritdoc />
    public string Name => "InMemory";

    /// <inheritdoc />
    public async IAsyncEnumerable<SourceMessage> GetMessagesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var msg in _messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return msg;
            await Task.Yield();
        }
    }
}
