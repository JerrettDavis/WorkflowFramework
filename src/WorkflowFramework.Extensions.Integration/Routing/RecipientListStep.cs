namespace WorkflowFramework.Extensions.Integration.Routing;

/// <summary>
/// Sends workflow data to multiple dynamic recipients determined at runtime.
/// </summary>
public sealed class RecipientListStep : IStep
{
    private readonly Func<IWorkflowContext, IEnumerable<IStep>> _recipientSelector;
    private readonly bool _parallel;

    /// <summary>
    /// Initializes a new instance of <see cref="RecipientListStep"/>.
    /// </summary>
    /// <param name="recipientSelector">Function that determines which recipients to send to.</param>
    /// <param name="parallel">Whether to send to recipients in parallel.</param>
    public RecipientListStep(Func<IWorkflowContext, IEnumerable<IStep>> recipientSelector, bool parallel = false)
    {
        _recipientSelector = recipientSelector ?? throw new ArgumentNullException(nameof(recipientSelector));
        _parallel = parallel;
    }

    /// <inheritdoc />
    public string Name => "RecipientList";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var recipients = _recipientSelector(context).ToList();

        if (_parallel)
        {
            await Task.WhenAll(recipients.Select(r => r.ExecuteAsync(context))).ConfigureAwait(false);
        }
        else
        {
            foreach (var recipient in recipients)
            {
                await recipient.ExecuteAsync(context).ConfigureAwait(false);
                if (context.IsAborted) break;
            }
        }
    }
}
