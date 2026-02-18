using WorkflowFramework.Extensions.Integration.Abstractions;

namespace WorkflowFramework.Extensions.Integration.Channel;

/// <summary>
/// Connects two different channel types by receiving from one and sending to another.
/// </summary>
public sealed class MessageBridgeStep : IStep
{
    private readonly IChannelAdapter _source;
    private readonly IChannelAdapter _destination;

    /// <summary>
    /// Initializes a new instance of <see cref="MessageBridgeStep"/>.
    /// </summary>
    /// <param name="source">The source channel to receive from.</param>
    /// <param name="destination">The destination channel to send to.</param>
    public MessageBridgeStep(IChannelAdapter source, IChannelAdapter destination)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _destination = destination ?? throw new ArgumentNullException(nameof(destination));
    }

    /// <inheritdoc />
    public string Name => "MessageBridge";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var message = await _source.ReceiveAsync(context.CancellationToken).ConfigureAwait(false);
        if (message != null)
        {
            await _destination.SendAsync(message, context.CancellationToken).ConfigureAwait(false);
        }
    }
}
