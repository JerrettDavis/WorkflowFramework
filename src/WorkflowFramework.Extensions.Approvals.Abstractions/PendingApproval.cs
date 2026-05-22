namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// An immutable snapshot of an in-flight or completed approval workflow persisted by an
/// <see cref="IApprovalStore"/>. Each property is init-only; create updated versions using
/// C# <c>with</c> expressions to maintain immutability.
/// </summary>
/// <param name="CorrelationId">
/// The unique identifier that links this record to its originating <see cref="ApprovalRequest"/>.
/// Must match <see cref="ApprovalRequest.CorrelationId"/>.
/// </param>
/// <param name="Request">
/// The original, immutable request that triggered this pending approval.
/// </param>
/// <param name="PrimaryChannel">
/// The <see cref="IApprovalChannel.Name"/> of the channel to which the request was initially
/// dispatched.
/// </param>
/// <param name="CreatedAt">
/// The point in time at which the request was first persisted, including time-zone offset.
/// </param>
/// <param name="DeadlineAt">
/// The absolute point in time after which the <see cref="OnTimeoutAction"/> will be applied.
/// Derived from <see cref="CreatedAt"/> plus <see cref="ApprovalRequest.Timeout"/>.
/// </param>
/// <param name="Votes">
/// The ordered, append-only list of votes collected so far. Use
/// <see cref="IApprovalStore.AppendVoteAsync"/> to add entries atomically.
/// </param>
/// <param name="EscalationChannel">
/// The <see cref="IApprovalChannel.Name"/> of the fallback channel to use when
/// <see cref="TimeoutAction"/> is <see cref="OnTimeoutAction.Escalate"/>.
/// May be <see langword="null"/> when escalation is not configured.
/// </param>
/// <param name="TimeoutAction">
/// Specifies the behaviour to apply when <see cref="DeadlineAt"/> is reached without
/// sufficient votes.
/// </param>
/// <param name="IsComplete">
/// <see langword="true"/> once the approval has reached a terminal state and
/// <see cref="Final"/> has been set; <see langword="false"/> while still awaiting votes.
/// Defaults to <see langword="false"/>.
/// </param>
/// <param name="Final">
/// The terminal <see cref="ApprovalResponse"/> recorded by
/// <see cref="IApprovalStore.CompleteAsync"/>. <see langword="null"/> while the request
/// is still in flight.
/// </param>
public sealed record PendingApproval(
    string CorrelationId,
    ApprovalRequest Request,
    string PrimaryChannel,
    DateTimeOffset CreatedAt,
    DateTimeOffset DeadlineAt,
    IReadOnlyList<ApprovalRecord> Votes,
    string? EscalationChannel,
    OnTimeoutAction TimeoutAction,
    bool IsComplete = false,
    ApprovalResponse? Final = null);
