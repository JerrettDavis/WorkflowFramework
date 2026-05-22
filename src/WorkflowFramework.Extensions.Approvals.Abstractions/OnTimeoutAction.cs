namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// Specifies what action the approval system should take when a request deadline is reached without
/// sufficient votes.
/// </summary>
public enum OnTimeoutAction
{
    /// <summary>
    /// Automatically close the request as rejected when the deadline passes.
    /// This is the safest default for security-sensitive workflows.
    /// </summary>
    AutoReject,

    /// <summary>
    /// Automatically close the request as approved when the deadline passes.
    /// Use only for low-risk, convenience-oriented workflows where a lack of response
    /// should be interpreted as implicit consent.
    /// </summary>
    AutoApprove,

    /// <summary>
    /// Forward the request to the configured escalation channel when the deadline passes.
    /// The escalation channel then becomes responsible for reaching a final decision.
    /// </summary>
    Escalate
}
