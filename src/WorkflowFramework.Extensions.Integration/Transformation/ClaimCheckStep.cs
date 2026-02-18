using WorkflowFramework.Extensions.Integration.Abstractions;

namespace WorkflowFramework.Extensions.Integration.Transformation;

/// <summary>
/// Stores a large payload externally and places a claim ticket in the workflow context.
/// </summary>
public sealed class ClaimCheckStep : IStep
{
    private readonly IClaimCheckStore _store;
    private readonly Func<IWorkflowContext, object> _payloadSelector;
    /// <summary>
    /// The property key used to store the claim ticket on the workflow context.
    /// </summary>
    public const string ClaimTicketKey = "__ClaimTicket";

    /// <summary>
    /// Initializes a new instance of <see cref="ClaimCheckStep"/>.
    /// </summary>
    /// <param name="store">The claim check store.</param>
    /// <param name="payloadSelector">Function to select the payload to store from the context.</param>
    public ClaimCheckStep(IClaimCheckStore store, Func<IWorkflowContext, object> payloadSelector)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _payloadSelector = payloadSelector ?? throw new ArgumentNullException(nameof(payloadSelector));
    }

    /// <inheritdoc />
    public string Name => "ClaimCheck";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var payload = _payloadSelector(context);
        var ticket = await _store.StoreAsync(payload, context.CancellationToken).ConfigureAwait(false);
        context.Properties[ClaimTicketKey] = ticket;
    }
}

/// <summary>
/// Retrieves a payload from the claim check store using the ticket in the workflow context.
/// </summary>
public sealed class ClaimRetrieveStep : IStep
{
    private readonly IClaimCheckStore _store;
    private readonly string _resultKey;

    /// <summary>
    /// Initializes a new instance of <see cref="ClaimRetrieveStep"/>.
    /// </summary>
    /// <param name="store">The claim check store.</param>
    /// <param name="resultKey">The property key to store the retrieved payload.</param>
    public ClaimRetrieveStep(IClaimCheckStore store, string resultKey = "__ClaimPayload")
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _resultKey = resultKey;
    }

    /// <inheritdoc />
    public string Name => "ClaimRetrieve";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var ticket = context.Properties[ClaimCheckStep.ClaimTicketKey] as string
            ?? throw new InvalidOperationException("No claim ticket found in context. Run ClaimCheckStep first.");

        var payload = await _store.RetrieveAsync(ticket, context.CancellationToken).ConfigureAwait(false);
        context.Properties[_resultKey] = payload;
    }
}
