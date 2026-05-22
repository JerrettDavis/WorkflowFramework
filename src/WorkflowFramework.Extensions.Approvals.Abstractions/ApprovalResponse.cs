namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// The final, immutable result returned by an <see cref="IApprovalChannel"/> once an
/// <see cref="ApprovalRequest"/> has reached a terminal state.
/// Use the static factory methods to construct well-formed instances.
/// </summary>
/// <param name="Approved">
/// <see langword="true"/> when the request was approved by the required number of approvers;
/// <see langword="false"/> for any other terminal state.
/// </param>
/// <param name="Reason">
/// A human-readable explanation of the outcome. May be <see langword="null"/> when
/// the outcome is self-explanatory (e.g., a clean approval).
/// </param>
/// <param name="Approvals">
/// The ordered list of individual votes collected before the terminal state was reached.
/// May be empty if no votes were cast (e.g., an immediate timeout).
/// </param>
public sealed record ApprovalResponse(
    bool Approved,
    string? Reason,
    IReadOnlyList<ApprovalRecord> Approvals)
{
    /// <summary>
    /// Gets or initialises the machine-readable outcome classification for this response.
    /// Defaults to <see cref="ApprovalOutcome.Pending"/> and should always be overridden
    /// by one of the factory methods.
    /// </summary>
    public ApprovalOutcome Outcome { get; init; }

    /// <summary>
    /// Creates a terminal approval response indicating that the required quorum was reached.
    /// </summary>
    /// <param name="records">
    /// All votes that were cast in favour of this request. Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// A new <see cref="ApprovalResponse"/> with <see cref="Approved"/> set to
    /// <see langword="true"/> and <see cref="Outcome"/> set to <see cref="ApprovalOutcome.Approved"/>.
    /// </returns>
    public static ApprovalResponse ApprovedBy(IReadOnlyList<ApprovalRecord> records) =>
        new(true, null, records) { Outcome = ApprovalOutcome.Approved };

    /// <summary>
    /// Creates a terminal response indicating that the request was explicitly rejected.
    /// </summary>
    /// <param name="reason">
    /// A human-readable explanation of why the request was rejected. May be
    /// <see langword="null"/> when no reason was supplied by the rejecting approver.
    /// </param>
    /// <param name="records">
    /// All votes cast before the rejection was finalised.
    /// </param>
    /// <returns>
    /// A new <see cref="ApprovalResponse"/> with <see cref="Approved"/> set to
    /// <see langword="false"/> and <see cref="Outcome"/> set to <see cref="ApprovalOutcome.Rejected"/>.
    /// </returns>
    public static ApprovalResponse Rejected(string? reason, IReadOnlyList<ApprovalRecord> records) =>
        new(false, reason, records) { Outcome = ApprovalOutcome.Rejected };

    /// <summary>
    /// Creates a terminal response indicating that the approval deadline elapsed before
    /// quorum was reached.
    /// </summary>
    /// <param name="partial">
    /// Any votes that were cast before the timeout, which may be an empty list.
    /// </param>
    /// <returns>
    /// A new <see cref="ApprovalResponse"/> with <see cref="Approved"/> set to
    /// <see langword="false"/>, <see cref="Reason"/> set to <c>"Timed out"</c>,
    /// and <see cref="Outcome"/> set to <see cref="ApprovalOutcome.TimedOut"/>.
    /// </returns>
    public static ApprovalResponse TimedOut(IReadOnlyList<ApprovalRecord> partial) =>
        new(false, "Timed out", partial) { Outcome = ApprovalOutcome.TimedOut };

    /// <summary>
    /// Creates a terminal response indicating that the request was forwarded to an escalation
    /// channel and should be considered unresolved at the primary channel level.
    /// </summary>
    /// <param name="partial">
    /// Any votes that were cast at the primary channel before escalation, which may be an
    /// empty list.
    /// </param>
    /// <returns>
    /// A new <see cref="ApprovalResponse"/> with <see cref="Approved"/> set to
    /// <see langword="false"/>, <see cref="Reason"/> set to <c>"Escalated"</c>,
    /// and <see cref="Outcome"/> set to <see cref="ApprovalOutcome.Escalated"/>.
    /// </returns>
    public static ApprovalResponse Escalated(IReadOnlyList<ApprovalRecord> partial) =>
        new(false, "Escalated", partial) { Outcome = ApprovalOutcome.Escalated };
}
