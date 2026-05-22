namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// Defines the persistence contract for tracking in-flight and completed approval requests.
/// Implementations are responsible for concurrency safety — particularly
/// <see cref="AppendVoteAsync"/>, which must update the vote list atomically to prevent
/// lost-update races in multi-approver scenarios.
/// </summary>
public interface IApprovalStore
{
    /// <summary>
    /// Persists a newly-created <see cref="PendingApproval"/> so that it can be retrieved,
    /// voted on, and eventually completed.
    /// </summary>
    /// <param name="pending">
    /// The pending approval to store. Its <see cref="PendingApproval.CorrelationId"/> must be
    /// unique within the store; implementations should throw if a duplicate is detected.
    /// </param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns>A <see cref="Task"/> that completes when the record has been durably stored.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pending"/> is <see langword="null"/>.</exception>
    Task SaveAsync(PendingApproval pending, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a <see cref="PendingApproval"/> by its correlation identifier.
    /// </summary>
    /// <param name="correlationId">
    /// The identifier originally set on the <see cref="ApprovalRequest"/> that created the record.
    /// </param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns>
    /// The matching <see cref="PendingApproval"/>, or <see langword="null"/> if no record with
    /// the given <paramref name="correlationId"/> exists.
    /// </returns>
    Task<PendingApproval?> LoadAsync(string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically appends a new <see cref="ApprovalRecord"/> vote to an existing
    /// <see cref="PendingApproval"/> and returns the updated record.
    /// Implementations must ensure that concurrent calls for the same
    /// <paramref name="correlationId"/> do not cause votes to be silently dropped.
    /// </summary>
    /// <param name="correlationId">The identifier of the pending approval to update.</param>
    /// <param name="vote">The vote to append. Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns>
    /// The updated <see cref="PendingApproval"/> with the new vote appended, allowing the caller
    /// to re-evaluate quorum without issuing a separate <see cref="LoadAsync"/> call.
    /// </returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no pending approval exists for the given <paramref name="correlationId"/>.
    /// </exception>
    Task<PendingApproval> AppendVoteAsync(string correlationId, ApprovalRecord vote, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all pending approvals that have not yet reached a terminal state
    /// (<see cref="PendingApproval.IsComplete"/> is <see langword="false"/>).
    /// Useful for timeout-sweep background services.
    /// </summary>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns>
    /// A read-only list of all in-flight <see cref="PendingApproval"/> records. May be empty
    /// if no approvals are currently pending.
    /// </returns>
    Task<IReadOnlyList<PendingApproval>> ListPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a pending approval as complete by recording its terminal
    /// <see cref="ApprovalResponse"/>.
    /// After this call, <see cref="PendingApproval.IsComplete"/> must return
    /// <see langword="true"/> and <see cref="PendingApproval.Final"/> must be non-null.
    /// </summary>
    /// <param name="correlationId">The identifier of the pending approval to complete.</param>
    /// <param name="final">
    /// The terminal response that summarises the outcome. Must not be <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns>A <see cref="Task"/> that completes once the record has been durably updated.</returns>
    /// <exception cref="KeyNotFoundException">
    /// Thrown when no pending approval exists for the given <paramref name="correlationId"/>.
    /// </exception>
    Task CompleteAsync(string correlationId, ApprovalResponse final, CancellationToken cancellationToken = default);
}
