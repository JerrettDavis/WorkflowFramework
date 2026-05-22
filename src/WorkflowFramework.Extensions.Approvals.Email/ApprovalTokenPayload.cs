namespace WorkflowFramework.Extensions.Approvals.Email;

/// <summary>
/// The data embedded in a signed approval callback token. Carries the minimum
/// information required to record a vote when the callback URL is visited.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the pending approval this token resolves.</param>
/// <param name="ApproverId">The unique identifier of the approver (typically their email address).</param>
/// <param name="Decision"><see langword="true"/> for an approval vote; <see langword="false"/> for a rejection.</param>
/// <param name="ExpiresAt">The absolute UTC moment after which the token must be rejected.</param>
/// <param name="ApproverDisplayName">An optional human-readable name for the approver, included in the audit record.</param>
public sealed record ApprovalTokenPayload(
    string CorrelationId,
    string ApproverId,
    bool Decision,
    DateTimeOffset ExpiresAt,
    string? ApproverDisplayName = null);
