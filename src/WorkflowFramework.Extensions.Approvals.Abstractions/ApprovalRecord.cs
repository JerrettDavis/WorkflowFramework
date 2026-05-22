namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// An immutable record of a single approver's vote on an <see cref="ApprovalRequest"/>.
/// </summary>
/// <param name="ApproverId">
/// The unique identifier of the approver (e.g., user ID, email address, or service account name).
/// </param>
/// <param name="ApproverDisplayName">
/// A human-readable display name for the approver, suitable for audit logs and notifications.
/// May be <see langword="null"/> when the display name cannot be resolved.
/// </param>
/// <param name="Approved">
/// <see langword="true"/> if this vote is an approval; <see langword="false"/> if it is a rejection.
/// </param>
/// <param name="Comment">
/// An optional free-text comment provided by the approver explaining their decision.
/// </param>
/// <param name="Timestamp">
/// The point in time at which the vote was recorded, including time-zone offset.
/// </param>
/// <param name="Channel">
/// The stable channel identifier (e.g., <c>"slack"</c>, <c>"email"</c>) through which
/// the vote was cast. Matches <see cref="IApprovalChannel.Name"/>.
/// </param>
public sealed record ApprovalRecord(
    string ApproverId,
    string? ApproverDisplayName,
    bool Approved,
    string? Comment,
    DateTimeOffset Timestamp,
    string Channel);
