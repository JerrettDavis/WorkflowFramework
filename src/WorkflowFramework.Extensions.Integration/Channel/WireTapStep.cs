namespace WorkflowFramework.Extensions.Integration.Channel;

/// <summary>
/// Inspects/copies messages flowing through without disrupting the pipeline (for audit/debug).
/// The tap action runs but any exceptions are swallowed to avoid disrupting the main flow.
/// </summary>
public sealed class WireTapStep : IStep
{
    private readonly Func<IWorkflowContext, Task> _tapAction;
    private readonly bool _swallowErrors;

    /// <summary>
    /// Initializes a new instance of <see cref="WireTapStep"/>.
    /// </summary>
    /// <param name="tapAction">The action to execute as a wire tap (e.g., logging, auditing).</param>
    /// <param name="swallowErrors">Whether to swallow errors from the tap action. Default true.</param>
    public WireTapStep(Func<IWorkflowContext, Task> tapAction, bool swallowErrors = true)
    {
        _tapAction = tapAction ?? throw new ArgumentNullException(nameof(tapAction));
        _swallowErrors = swallowErrors;
    }

    /// <inheritdoc />
    public string Name => "WireTap";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        if (_swallowErrors)
        {
            try
            {
                await _tapAction(context).ConfigureAwait(false);
            }
            catch
            {
                // Wire tap should not affect main flow
            }
        }
        else
        {
            await _tapAction(context).ConfigureAwait(false);
        }
    }
}
