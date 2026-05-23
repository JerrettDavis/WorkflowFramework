namespace WorkflowFramework.Extensions.Integration.Abstractions;

/// <summary>
/// Stores and retrieves large payloads using claim check pattern.
/// </summary>
/// <remarks>
/// <b>DEPRECATED:</b> Use <c>PatternKit.Messaging.Transformation.IClaimCheckStore&lt;TPayload&gt;</c>
/// directly. This interface is retained for one release as a back-compat shim and will be removed
/// in the next major version. Migrate to <c>IClaimCheckStore&lt;object&gt;</c> (or a typed variant)
/// and update DI registrations accordingly.
/// See <c>LegacyClaimCheckStoreAdapter</c> for a bridge between the old and new contracts.
/// </remarks>
[Obsolete(
    "WorkflowFramework.Extensions.Integration.Abstractions.IClaimCheckStore is obsolete. " +
    "Migrate to PatternKit.Messaging.Transformation.IClaimCheckStore<TPayload> " +
    "(use IClaimCheckStore<object> for untyped payloads). " +
    "A legacy adapter LegacyClaimCheckStoreAdapter is available for one release. " +
    "This interface will be removed in the next major version.",
    error: false)]
public interface IClaimCheckStore
{
    /// <summary>
    /// Stores a payload and returns a claim ticket identifier.
    /// </summary>
    /// <param name="payload">The payload to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A claim ticket that can be used to retrieve the payload later.</returns>
    Task<string> StoreAsync(object payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a payload using its claim ticket.
    /// </summary>
    /// <param name="claimTicket">The claim ticket identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored payload.</returns>
    Task<object> RetrieveAsync(string claimTicket, CancellationToken cancellationToken = default);
}
