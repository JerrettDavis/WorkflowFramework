namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// Represents the final outcome of an approval request lifecycle.
/// </summary>
public enum ApprovalOutcome
{
    /// <summary>
    /// The approval request has been submitted but no decision has been reached yet.
    /// </summary>
    Pending,

    /// <summary>
    /// The required number of approvers have approved the request.
    /// </summary>
    Approved,

    /// <summary>
    /// The request was explicitly rejected by one or more approvers.
    /// </summary>
    Rejected,

    /// <summary>
    /// The approval deadline elapsed before the required number of votes were collected.
    /// </summary>
    TimedOut,

    /// <summary>
    /// The request was forwarded to an escalation channel because the primary channel could not reach quorum in time.
    /// </summary>
    Escalated
}
