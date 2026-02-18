using WorkflowFramework.Extensions.Integration.Abstractions;

namespace WorkflowFramework.Extensions.Integration.Channel;

/// <summary>
/// Routes failed or unprocessable items to a dead letter store.
/// </summary>
public sealed class DeadLetterStep : IStep
{
    private readonly IDeadLetterStore _store;
    private readonly IStep _innerStep;

    /// <summary>
    /// Initializes a new instance of <see cref="DeadLetterStep"/>.
    /// </summary>
    /// <param name="store">The dead letter store.</param>
    /// <param name="innerStep">The step to wrap — failures are routed to dead letter.</param>
    public DeadLetterStep(IDeadLetterStore store, IStep innerStep)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _innerStep = innerStep ?? throw new ArgumentNullException(nameof(innerStep));
    }

    /// <inheritdoc />
    public string Name => "DeadLetter";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        try
        {
            await _innerStep.ExecuteAsync(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var message = context.Properties.ContainsKey("__CurrentMessage")
                ? context.Properties["__CurrentMessage"]!
                : context;

            await _store.SendAsync(message, ex.Message, ex, context.CancellationToken).ConfigureAwait(false);
        }
    }
}
