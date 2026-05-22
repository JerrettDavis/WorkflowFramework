// Intentionally bespoke — PatternKit AsyncAdapter<TIn,TOut> is a type-mapping pattern
// (produce a TOut from a TIn). ChannelAdapterStep is a side-effect operation (send a
// message to an IChannelAdapter); it has no output type and no conversion pipeline.
// Using AsyncAdapter here would be a category error. Characterization tests added in
// Phase G.3.
using WorkflowFramework.Extensions.Integration.Abstractions;

namespace WorkflowFramework.Extensions.Integration.Channel;

/// <summary>
/// Bridges between a workflow and an external messaging system.
/// </summary>
public sealed class ChannelAdapterStep : IStep
{
    private readonly IChannelAdapter _adapter;
    private readonly Func<IWorkflowContext, object> _messageSelector;

    /// <summary>
    /// Initializes a new instance of <see cref="ChannelAdapterStep"/>.
    /// </summary>
    /// <param name="adapter">The channel adapter.</param>
    /// <param name="messageSelector">Function to extract the message to send from context.</param>
    public ChannelAdapterStep(IChannelAdapter adapter, Func<IWorkflowContext, object> messageSelector)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _messageSelector = messageSelector ?? throw new ArgumentNullException(nameof(messageSelector));
    }

    /// <inheritdoc />
    public string Name => "ChannelAdapter";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var message = _messageSelector(context);
        await _adapter.SendAsync(message, context.CancellationToken).ConfigureAwait(false);
    }
}
