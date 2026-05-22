namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// Resolves the appropriate <see cref="IApprovalChannel"/> for a given
/// <see cref="ApprovalRequest"/> based on routing rules defined by the implementing class.
/// </summary>
/// <remarks>
/// Implementations may inspect any property of the <see cref="ApprovalRequest"/> —
/// including <see cref="ApprovalRequest.AllowedRoles"/>, context values, or the
/// <see cref="ApprovalRequest.Title"/> — to select between registered channels.
/// A common pattern is to read a <c>"channel"</c> key from
/// <see cref="ApprovalRequest.Context"/> and resolve it by name.
/// </remarks>
public interface IApprovalRouter
{
    /// <summary>
    /// Resolves the <see cref="IApprovalChannel"/> that should handle the given
    /// <paramref name="request"/>.
    /// </summary>
    /// <param name="request">
    /// The approval request for which a channel must be selected.
    /// Must not be <see langword="null"/>.
    /// </param>
    /// <returns>
    /// The <see cref="IApprovalChannel"/> selected to deliver and track this request.
    /// Implementations must never return <see langword="null"/>; throw an exception if
    /// no channel can be resolved.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="request"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no suitable channel can be found for the given request.
    /// </exception>
    IApprovalChannel Resolve(ApprovalRequest request);
}
