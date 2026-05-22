namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// Configuration options that supply defaults for the approvals orchestrator. Individual
/// <see cref="ApprovalRequest"/> properties override these values when explicitly set.
/// </summary>
public sealed class ApprovalsOptions
{
    /// <summary>
    /// Gets or sets the minimum number of approvers required to consider a request approved
    /// when no per-request override is given. Must be greater than or equal to 1.
    /// Defaults to <c>1</c>.
    /// </summary>
    public int RequiredApprovers { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum time to wait for votes before the <see cref="OnTimeoutAction"/>
    /// is applied when no per-request timeout is specified.
    /// Defaults to 24 hours.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets the action to take when an approval request reaches its deadline without
    /// sufficient votes.
    /// Defaults to <see cref="OnTimeoutAction.AutoReject"/> (safest for security-sensitive workflows).
    /// </summary>
    public OnTimeoutAction OnTimeoutAction { get; set; } = OnTimeoutAction.AutoReject;
}
