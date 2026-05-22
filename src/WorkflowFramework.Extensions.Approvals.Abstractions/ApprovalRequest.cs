namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// An immutable description of an approval request sent to an <see cref="IApprovalChannel"/>.
/// Instances should be created via <see cref="ApprovalRequestBuilder"/> to ensure all
/// validation constraints are satisfied before dispatch.
/// </summary>
/// <param name="Title">
/// A short, human-readable title displayed as the subject of the approval notification.
/// </param>
/// <param name="Description">
/// An optional longer description providing context for approvers. May contain markdown
/// depending on the channel's rendering capabilities.
/// </param>
/// <param name="Context">
/// An arbitrary key-value bag of contextual data attached to the request (e.g., deployment
/// commit SHA, order ID, environment name). Values are serialized by each channel implementation
/// as appropriate for its notification format.
/// </param>
/// <param name="RequiredApprovers">
/// The minimum number of approvers who must approve before the request is considered
/// approved. Must be greater than or equal to one.
/// </param>
/// <param name="Timeout">
/// The maximum time to wait for approvals before the <see cref="OnTimeoutAction"/> is applied.
/// Must be a positive <see cref="TimeSpan"/>.
/// </param>
/// <param name="AllowedRoles">
/// An optional allow-list of role names. When non-null, only users holding one of the listed
/// roles may cast an approving or rejecting vote. A <see langword="null"/> value means any
/// authenticated user may vote.
/// </param>
public sealed record ApprovalRequest(
    string Title,
    string? Description,
    IReadOnlyDictionary<string, object?> Context,
    int RequiredApprovers,
    TimeSpan Timeout,
    IReadOnlyList<string>? AllowedRoles)
{
    /// <summary>
    /// Gets or initialises a correlation identifier that uniquely links this request to its
    /// <see cref="PendingApproval"/> record in the <see cref="IApprovalStore"/>.
    /// Defaults to a new <see cref="Guid"/> formatted as a 32-character hex string
    /// (format specifier <c>"N"</c>) when not explicitly supplied.
    /// </summary>
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");
}
