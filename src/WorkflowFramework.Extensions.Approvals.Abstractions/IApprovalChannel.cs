namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// Represents a delivery mechanism capable of sending an <see cref="ApprovalRequest"/> to
/// human approvers and waiting for their response.
/// Implementations include Slack, Microsoft Teams, email, and interactive CLI prompts.
/// </summary>
public interface IApprovalChannel
{
    /// <summary>
    /// Gets the stable, lowercase identifier for this channel.
    /// This value is used by routing logic (see <see cref="IApprovalRouter"/>),
    /// escalation configuration, and is recorded on each <see cref="ApprovalRecord"/>
    /// cast through this channel.
    /// </summary>
    /// <example>
    /// Typical values: <c>"slack"</c>, <c>"teams"</c>, <c>"email"</c>, <c>"cli"</c>.
    /// </example>
    string Name { get; }

    /// <summary>
    /// Sends the <paramref name="request"/> to approvers via this channel and asynchronously
    /// waits until the request reaches a terminal state (approved, rejected, timed out, or
    /// escalated).
    /// </summary>
    /// <param name="request">
    /// The fully-constructed approval request describing the action that requires human sign-off.
    /// Must not be <see langword="null"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that, when cancelled, should cause the in-flight request to be abandoned and
    /// a faulted or cancelled task to be returned. Channel implementations must honour this
    /// token.
    /// </param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that resolves to an <see cref="ApprovalResponse"/>
    /// describing the terminal outcome once a decision is reached or the timeout elapses.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="request"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled before a terminal state
    /// is reached.
    /// </exception>
    Task<ApprovalResponse> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default);
}
