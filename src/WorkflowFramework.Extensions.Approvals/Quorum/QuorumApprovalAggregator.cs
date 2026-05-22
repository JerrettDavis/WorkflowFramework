namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// Pure, stateless quorum evaluation logic for N-of-M approval scenarios.
/// No dependencies; all methods are <see langword="static"/>.
/// </summary>
public static class QuorumApprovalAggregator
{
    /// <summary>
    /// Evaluates the current vote state and determines whether quorum has been reached,
    /// is still reachable, or is mathematically impossible.
    /// </summary>
    /// <param name="votes">
    /// All votes cast so far. Must not be <see langword="null"/>. Each vote's
    /// <see cref="ApprovalRecord.Approved"/> property indicates whether it is an
    /// approval (<see langword="true"/>) or a rejection (<see langword="false"/>).
    /// </param>
    /// <param name="requiredApprovers">
    /// The minimum number of approvals needed to reach an <see cref="ApprovalOutcome.Approved"/>
    /// outcome. Must be greater than or equal to 1.
    /// </param>
    /// <param name="totalAddressableApprovers">
    /// The total number of distinct approvers who could potentially cast a vote.
    /// Must be greater than or equal to <paramref name="requiredApprovers"/>.
    /// </param>
    /// <returns>
    /// <list type="bullet">
    ///   <item><see cref="ApprovalOutcome.Approved"/> — quorum has been reached.</item>
    ///   <item><see cref="ApprovalOutcome.Rejected"/> — quorum is mathematically impossible
    ///   given the remaining votes available.</item>
    ///   <item><see cref="ApprovalOutcome.Pending"/> — more votes are needed; the outcome
    ///   is still undecided.</item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="votes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="requiredApprovers"/> is less than or equal to 0, or when
    /// <paramref name="totalAddressableApprovers"/> is less than <paramref name="requiredApprovers"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <c>votes.Count</c> exceeds <paramref name="totalAddressableApprovers"/>.
    /// </exception>
    public static ApprovalOutcome Evaluate(
        IReadOnlyList<ApprovalRecord> votes,
        int requiredApprovers,
        int totalAddressableApprovers)
    {
        if (votes is null)
            throw new ArgumentNullException(nameof(votes));

        if (requiredApprovers <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(requiredApprovers),
                requiredApprovers,
                "Required approvers must be greater than or equal to 1.");

        if (totalAddressableApprovers < requiredApprovers)
            throw new ArgumentOutOfRangeException(
                nameof(totalAddressableApprovers),
                totalAddressableApprovers,
                $"Total addressable approvers ({totalAddressableApprovers}) must be >= requiredApprovers ({requiredApprovers}).");

        if (votes.Count > totalAddressableApprovers)
            throw new ArgumentException(
                $"Vote count ({votes.Count}) exceeds totalAddressableApprovers ({totalAddressableApprovers}).",
                nameof(votes));

        var approvals = votes.Count(v => v.Approved);

        // Quorum reached.
        if (approvals >= requiredApprovers)
            return ApprovalOutcome.Approved;

        // Can the remaining non-voters push approvals over the threshold?
        var remaining = totalAddressableApprovers - votes.Count;
        if (approvals + remaining < requiredApprovers)
            return ApprovalOutcome.Rejected;

        return ApprovalOutcome.Pending;
    }
}
