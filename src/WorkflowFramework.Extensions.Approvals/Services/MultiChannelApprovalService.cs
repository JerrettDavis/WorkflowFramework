namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// The top-level facade <see cref="IApprovalChannel"/> that composes the routing, persistence,
/// and timeout/escalation pipeline into a single coherent channel.
/// </summary>
/// <remarks>
/// <para>
/// The fully-composed pipeline (router → persistence → escalation) is injected as a single
/// <see cref="IApprovalChannel"/> at construction time via the DI builder.  This keeps the
/// public surface clean and allows each layer to be tested independently.
/// </para>
/// <para>
/// <see cref="Name"/> is always <c>"approvals"</c>.
/// </para>
/// </remarks>
public sealed class MultiChannelApprovalService : IApprovalChannel
{
    private readonly IApprovalChannel _pipeline;

    /// <summary>
    /// Initialises a new instance of <see cref="MultiChannelApprovalService"/>.
    /// </summary>
    /// <param name="pipeline">
    /// The composed pipeline produced by the DI builder. Must not be <see langword="null"/>.
    /// </param>
    public MultiChannelApprovalService(IApprovalChannel pipeline)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    }

    /// <inheritdoc />
    public string Name => "approvals";

    /// <inheritdoc />
    public Task<ApprovalResponse> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
        => _pipeline.RequestApprovalAsync(request, cancellationToken);
}
