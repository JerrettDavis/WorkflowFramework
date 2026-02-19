using System.Threading.Channels;
using WorkflowFramework.Samples.TaskStream.Models;

namespace WorkflowFramework.Samples.TaskStream.Sources;

/// <summary>
/// A task source backed by a <see cref="Channel{T}"/>. External code (e.g., a minimal API endpoint)
/// writes messages to the channel, and the source reads from it.
/// </summary>
public sealed class WebhookTaskSource : ITaskSource
{
    private readonly Channel<SourceMessage> _channel = Channel.CreateUnbounded<SourceMessage>();

    /// <inheritdoc />
    public string Name => "Webhook";

    /// <summary>Gets the writer for pushing messages into this source.</summary>
    public ChannelWriter<SourceMessage> Writer => _channel.Writer;

    /// <inheritdoc />
    public async IAsyncEnumerable<SourceMessage> GetMessagesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (_channel.Reader.TryRead(out var message))
            {
                yield return message;
            }
        }
    }
}
