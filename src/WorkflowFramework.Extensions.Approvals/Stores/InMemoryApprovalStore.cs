using System.Collections.Concurrent;

namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// A thread-safe, in-memory implementation of <see cref="IApprovalStore"/>.
/// Suitable for testing and single-process deployments. All state is lost on process restart;
/// use a durable store (e.g., EF Core or Redis) for production scenarios that require
/// crash recovery.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AppendVoteAsync"/> is fully atomic — it uses
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> compare-and-swap semantics to ensure that
/// concurrent votes from multiple approvers cannot silently overwrite each other.
/// </para>
/// <para>
/// If the same <c>ApproverId</c> submits more than one vote for the same correlation, the
/// <em>last</em> vote wins (idempotent upsert). This prevents double-counting when a channel
/// implementation retries delivery.
/// </para>
/// </remarks>
public sealed class InMemoryApprovalStore : IApprovalStore
{
    private readonly ConcurrentDictionary<string, PendingApproval> _store =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pending"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a record with the same <see cref="PendingApproval.CorrelationId"/> already exists.</exception>
    public Task SaveAsync(PendingApproval pending, CancellationToken cancellationToken = default)
    {
        if (pending is null) throw new ArgumentNullException(nameof(pending));

        if (!_store.TryAdd(pending.CorrelationId, pending))
            throw new InvalidOperationException(
                $"A pending approval with CorrelationId '{pending.CorrelationId}' already exists.");

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<PendingApproval?> LoadAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(correlationId, out var pending);
        return Task.FromResult(pending);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// If a vote from the same <paramref name="vote"/>.<c>ApproverId</c> already exists for
    /// this correlation, the previous vote is <em>replaced</em> by the new one (last write wins
    /// for the same approver). This ensures that re-sent or retried votes are idempotent.
    /// </para>
    /// <para>
    /// The method spins on an optimistic compare-and-swap loop so that concurrent votes from
    /// <em>different</em> approvers never silently drop each other.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no pending approval exists for <paramref name="correlationId"/>, or when the
    /// existing record has already been completed (<see cref="PendingApproval.IsComplete"/> is
    /// <see langword="true"/>).
    /// </exception>
    public Task<PendingApproval> AppendVoteAsync(
        string correlationId,
        ApprovalRecord vote,
        CancellationToken cancellationToken = default)
    {
        while (true)
        {
            if (!_store.TryGetValue(correlationId, out var current))
                throw new InvalidOperationException(
                    $"No pending approval found for CorrelationId '{correlationId}'.");

            if (current.IsComplete)
                throw new InvalidOperationException(
                    $"The approval '{correlationId}' is already complete and cannot accept further votes.");

            // Build updated vote list — replace existing vote from same approver (idempotent upsert).
            var existingVotes = current.Votes.Where(v => v.ApproverId != vote.ApproverId).ToList();
            existingVotes.Add(vote);

            var updated = current with { Votes = existingVotes.AsReadOnly() };

            // Atomic CAS: only swap if the record hasn't changed since we read it.
            if (_store.TryUpdate(correlationId, updated, current))
                return Task.FromResult(updated);

            // Another thread modified the record — spin and retry.
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PendingApproval>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PendingApproval> result = _store.Values
            .Where(p => !p.IsComplete)
            .ToList()
            .AsReadOnly();

        return Task.FromResult(result);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown when no pending approval exists for <paramref name="correlationId"/>.
    /// </exception>
    public Task CompleteAsync(
        string correlationId,
        ApprovalResponse final,
        CancellationToken cancellationToken = default)
    {
        if (!_store.TryGetValue(correlationId, out var current))
            throw new InvalidOperationException(
                $"No pending approval found for CorrelationId '{correlationId}'.");

        var completed = current with { IsComplete = true, Final = final };
        _store[correlationId] = completed;

        return Task.CompletedTask;
    }
}
