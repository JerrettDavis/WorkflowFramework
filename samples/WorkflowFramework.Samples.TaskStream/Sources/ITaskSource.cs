using WorkflowFramework.Samples.TaskStream.Models;

namespace WorkflowFramework.Samples.TaskStream.Sources;

/// <summary>
/// Represents a source of incoming messages that may contain tasks.
/// </summary>
public interface ITaskSource
{
    /// <summary>Gets the source name.</summary>
    string Name { get; }

    /// <summary>
    /// Retrieves messages from this source.
    /// </summary>
    IAsyncEnumerable<SourceMessage> GetMessagesAsync(CancellationToken cancellationToken = default);
}
