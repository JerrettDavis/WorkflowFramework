using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;

namespace WorkflowFramework.Extensions.Integration.Abstractions;

/// <summary>
/// Bridges the deprecated <see cref="IOutboxStore"/> (untyped, WF bespoke) to
/// <see cref="IOutboxStore{TPayload}">PatternKit IOutboxStore&lt;object&gt;</see>.
/// </summary>
/// <remarks>
/// <b>DEPRECATED:</b> This adapter is provided for one release only. It allows consumers of the old
/// untyped <see cref="IOutboxStore"/> to integrate with steps that now consume
/// <c>IOutboxStore&lt;object&gt;</c> without requiring an immediate migration.
/// Consumers should migrate their implementations directly to <c>IOutboxStore&lt;object&gt;</c>
/// and remove the legacy interface and this adapter in the next major version.
/// </remarks>
[Obsolete(
    "LegacyOutboxStoreAdapter is a one-release back-compat bridge. " +
    "Implement PatternKit.Messaging.Reliability.IOutboxStore<object> directly " +
    "and remove this adapter in the next major version.",
    error: false)]
public sealed class LegacyOutboxStoreAdapter : IOutboxStore<object>
{
#pragma warning disable CS0618 // suppress inner use of obsolete IOutboxStore
    private readonly IOutboxStore _legacy;

    /// <summary>
    /// Wraps a legacy <see cref="IOutboxStore"/> as a typed <see cref="IOutboxStore{TPayload}"/>.
    /// </summary>
    public LegacyOutboxStoreAdapter(IOutboxStore legacy)
    {
        _legacy = legacy ?? throw new ArgumentNullException(nameof(legacy));
    }
#pragma warning restore CS0618

    /// <inheritdoc />
    public async ValueTask<OutboxMessage<object>> EnqueueAsync(
        Message<object> message,
        string? id = null,
        DateTimeOffset? createdAt = null,
        CancellationToken cancellationToken = default)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        // Delegate to legacy store; it returns a string ID.
        var legacyId = await _legacy.SaveAsync(message.Payload, cancellationToken).ConfigureAwait(false);
        var effectiveId = string.IsNullOrWhiteSpace(id) ? legacyId : id!;
        var record = new OutboxMessage<object>(effectiveId, message, createdAt ?? DateTimeOffset.UtcNow);
        return record;
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<OutboxMessage<object>>> SnapshotPendingAsync(CancellationToken cancellationToken = default)
    {
        var legacyPending = await _legacy.GetPendingAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return legacyPending.Select(lm =>
        {
            var msg = new Message<object>(lm.Payload, MessageHeaders.Empty);
            return new OutboxMessage<object>(lm.Id, msg, lm.CreatedAt);
        }).ToArray();
    }

    /// <inheritdoc />
    public ValueTask MarkDispatchedAsync(string id, DateTimeOffset dispatchedAt, CancellationToken cancellationToken = default)
        => new(_legacy.MarkAsSentAsync(id, cancellationToken));

    /// <inheritdoc />
    public ValueTask MarkFailedAsync(string id, string? error, CancellationToken cancellationToken = default)
        // Legacy interface has no MarkFailedAsync; no-op for one release.
        => default;
}
