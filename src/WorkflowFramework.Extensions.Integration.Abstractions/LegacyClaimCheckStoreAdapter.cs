using PatternKit.Messaging;
using PatternKit.Messaging.Transformation;

namespace WorkflowFramework.Extensions.Integration.Abstractions;

/// <summary>
/// Bridges the deprecated <see cref="IClaimCheckStore"/> (untyped, WF bespoke) to
/// <see cref="IClaimCheckStore{TPayload}">IClaimCheckStore&lt;object&gt;</see> (typed, PatternKit 0.113+).
/// </summary>
/// <remarks>
/// <b>DEPRECATED:</b> This adapter is provided for one release only. It allows consumers of the old
/// untyped <see cref="IClaimCheckStore"/> to integrate with steps that now consume
/// <c>IClaimCheckStore&lt;object&gt;</c> without requiring an immediate migration.
/// Consumers should migrate their implementations directly to <c>IClaimCheckStore&lt;object&gt;</c>
/// and remove the legacy interface and this adapter in the next major version.
/// </remarks>
[Obsolete(
    "LegacyClaimCheckStoreAdapter is a one-release back-compat bridge. " +
    "Implement PatternKit.Messaging.Transformation.IClaimCheckStore<object> directly " +
    "and remove this adapter in the next major version.",
    error: false)]
public sealed class LegacyClaimCheckStoreAdapter : IClaimCheckStore<object>
{
#pragma warning disable CS0618 // suppress inner use of obsolete IClaimCheckStore
    private readonly IClaimCheckStore _legacy;

    /// <summary>
    /// Wraps a legacy <see cref="IClaimCheckStore"/> as a typed <see cref="IClaimCheckStore{TPayload}"/>.
    /// </summary>
    public LegacyClaimCheckStoreAdapter(IClaimCheckStore legacy)
    {
        _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
    }
#pragma warning restore CS0618

    /// <inheritdoc />
    public async ValueTask StoreAsync(
        string claimId,
        object payload,
        MessageHeaders headers,
        CancellationToken cancellationToken = default)
    {
        // Legacy contract returns the ticket from StoreAsync; we accept claimId from the caller
        // and discard the store-generated ticket (WF steps now generate their own deterministic IDs).
        await _legacy.StoreAsync(payload, cancellationToken).ConfigureAwait(false);
        // Store under the provided claimId by doing a second put via the typed path.
        // Since legacy store does not accept a caller-supplied ID, we must work around this:
        // store returns its own ticket which we cannot override. This adapter therefore maintains
        // an internal typed store keyed by the WF-supplied claimId that shadows the legacy store.
        // Subsequent TryLoadAsync will use this shadow store.
        _shadow[claimId] = new ClaimCheckStoredPayload<object>(payload, headers);
    }

    /// <inheritdoc />
    public ValueTask<ClaimCheckStoredPayload<object>?> TryLoadAsync(
        string claimId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _shadow.TryGetValue(claimId, out var stored);
        return new ValueTask<ClaimCheckStoredPayload<object>?>(stored);
    }

    // Internal shadow dict keyed by WF-generated claimId (legacy store has no ID-aware API).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ClaimCheckStoredPayload<object>>
        _shadow = new(StringComparer.Ordinal);
}
