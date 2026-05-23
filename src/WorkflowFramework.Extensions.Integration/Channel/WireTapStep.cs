// Refactored in feat/consume-patternkit-0.112 to delegate to
// PatternKit.Messaging.Channels.AsyncWireTap<IWorkflowContext>.
// The step wraps the caller-supplied Func<IWorkflowContext, Task> into a
// PatternKit tap handler, maps swallowErrors to TapErrorPolicy, and exposes
// the same constructor signature and Name property as before.
// All Phase G.3 characterization tests pass without modification.
using PatternKit.Messaging;
using PatternKit.Messaging.Channels;

namespace WorkflowFramework.Extensions.Integration.Channel;

/// <summary>
/// Inspects/copies messages flowing through without disrupting the pipeline (for audit/debug).
/// The tap action runs but any exceptions are swallowed to avoid disrupting the main flow.
/// </summary>
public sealed class WireTapStep : IStep
{
    private readonly AsyncWireTap<IWorkflowContext> _wireTap;

    /// <summary>
    /// Initializes a new instance of <see cref="WireTapStep"/>.
    /// </summary>
    /// <param name="tapAction">The action to execute as a wire tap (e.g., logging, auditing).</param>
    /// <param name="swallowErrors">Whether to swallow errors from the tap action. Default true.</param>
    public WireTapStep(Func<IWorkflowContext, Task> tapAction, bool swallowErrors = true)
    {
        if (tapAction is null) throw new ArgumentNullException(nameof(tapAction));

        var policy = swallowErrors ? TapErrorPolicy.Swallow : TapErrorPolicy.Propagate;

        _wireTap = AsyncWireTap<IWorkflowContext>.Create("wire-tap")
            .Tap("tap", async (message, _, ct) =>
            {
                await tapAction(message.Payload).ConfigureAwait(false);
            }, policy)
            .Build();
    }

    /// <inheritdoc />
    public string Name => "WireTap";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var message = new Message<IWorkflowContext>(context);
        await _wireTap.PublishAsync(message, cancellationToken: context.CancellationToken).ConfigureAwait(false);
    }
}
