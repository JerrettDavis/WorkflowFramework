namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// An <see cref="IApprovalRouter"/> implementation that always returns the same
/// <see cref="IApprovalChannel"/> regardless of the request content.
/// Useful in single-channel deployments where routing logic is unnecessary.
/// </summary>
public sealed class SingleChannelRouter : IApprovalRouter
{
    private readonly IApprovalChannel _channel;

    /// <summary>
    /// Initialises a new instance of <see cref="SingleChannelRouter"/>.
    /// </summary>
    /// <param name="channel">
    /// The channel to return for every request. Must not be <see langword="null"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="channel"/> is <see langword="null"/>.</exception>
    public SingleChannelRouter(IApprovalChannel channel)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Always returns the single channel supplied at construction time.
    /// The <paramref name="request"/> parameter is accepted but ignored.
    /// </remarks>
    public IApprovalChannel Resolve(ApprovalRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        return _channel;
    }
}
