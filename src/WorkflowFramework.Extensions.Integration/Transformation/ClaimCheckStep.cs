using PatternKit.Messaging;
using PatternKit.Messaging.Transformation;

namespace WorkflowFramework.Extensions.Integration.Transformation;

/// <summary>
/// Stores a large payload externally and places a claim ticket in the workflow context.
/// Internally delegates to <see cref="IClaimCheckStore{TPayload}">PatternKit IClaimCheckStore&lt;object&gt;</see>.
/// </summary>
/// <remarks>
/// The step generates a deterministic claim ID using <see cref="Guid.NewGuid"/> (formatted as N).
/// The claim ID is written to <see cref="ClaimTicketKey"/> on the context and used as the store key.
/// </remarks>
public sealed class ClaimCheckStep : IStep
{
    private readonly IClaimCheckStore<object> _store;
    private readonly Func<IWorkflowContext, object> _payloadSelector;

    /// <summary>
    /// The property key used to store the claim ticket on the workflow context.
    /// </summary>
    public const string ClaimTicketKey = "__ClaimTicket";

    /// <summary>
    /// Initializes a new instance of <see cref="ClaimCheckStep"/> consuming
    /// <see cref="IClaimCheckStore{TPayload}">PatternKit IClaimCheckStore&lt;object&gt;</see>.
    /// </summary>
    /// <param name="store">The PatternKit typed claim check store.</param>
    /// <param name="payloadSelector">Function to select the payload to store from the context.</param>
    public ClaimCheckStep(IClaimCheckStore<object> store, Func<IWorkflowContext, object> payloadSelector)
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
        var claimId = Guid.NewGuid().ToString("N");
        await _store.StoreAsync(claimId, payload, MessageHeaders.Empty, context.CancellationToken).ConfigureAwait(false);
        context.Properties[ClaimTicketKey] = claimId;
    }
}

/// <summary>
/// Retrieves a payload from the claim check store using the ticket in the workflow context.
/// Internally delegates to <see cref="IClaimCheckStore{TPayload}">PatternKit IClaimCheckStore&lt;object&gt;</see>.
/// </summary>
public sealed class ClaimRetrieveStep : IStep
{
    private readonly IClaimCheckStore<object> _store;
    private readonly string _resultKey;

    /// <summary>
    /// Initializes a new instance of <see cref="ClaimRetrieveStep"/> consuming
    /// <see cref="IClaimCheckStore{TPayload}">PatternKit IClaimCheckStore&lt;object&gt;</see>.
    /// </summary>
    /// <param name="store">The PatternKit typed claim check store.</param>
    /// <param name="resultKey">The property key to store the retrieved payload.</param>
    public ClaimRetrieveStep(IClaimCheckStore<object> store, string resultKey = "__ClaimPayload")
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

        var stored = await _store.TryLoadAsync(ticket, context.CancellationToken).ConfigureAwait(false);
        if (stored is null)
            throw new InvalidOperationException($"Claim '{ticket}' was not found in the store. The payload may have expired or was never stored.");

        context.Properties[_resultKey] = stored.Payload;
    }
}
