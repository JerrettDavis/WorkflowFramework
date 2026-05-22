using WorkflowFramework.Extensions.Scheduling;

namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// Bridges the new approvals orchestrator to the legacy
/// <see cref="IApprovalService"/> interface consumed by
/// <c>WorkflowFramework.Extensions.Scheduling</c>, allowing scheduling-based workflows
/// to transparently benefit from the full approval pipeline (persistence, quorum, escalation)
/// without code changes on their side.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RequestApprovalAsync"/> translates an <see cref="ApprovalConfig"/> into an
/// <see cref="ApprovalRequest"/> using the workflow ID as the correlation identifier, then
/// returns <see langword="true"/> when the orchestrator approves and <see langword="false"/>
/// otherwise.
/// </para>
/// <para>
/// <see cref="Approve"/> and <see cref="Reject"/> forward a synthesised
/// <see cref="ApprovalRecord"/> (approver = <c>"manual:scheduling"</c>) to
/// <see cref="PersistentApprovalService.ResolveExternalAsync"/>.
/// </para>
/// </remarks>
public sealed class SchedulingApprovalServiceAdapter : IApprovalService
{
    private readonly IApprovalChannel _channel;
    private readonly PersistentApprovalService _persistentService;

    /// <summary>
    /// Initialises a new instance of <see cref="SchedulingApprovalServiceAdapter"/>.
    /// </summary>
    /// <param name="channel">
    /// The top-level approval channel (typically <see cref="MultiChannelApprovalService"/>).
    /// Must not be <see langword="null"/>.
    /// </param>
    /// <param name="persistentService">
    /// The persistent service used to resolve manual approve/reject calls.
    /// Must not be <see langword="null"/>.
    /// </param>
    public SchedulingApprovalServiceAdapter(
        IApprovalChannel channel,
        PersistentApprovalService persistentService)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _persistentService = persistentService ?? throw new ArgumentNullException(nameof(persistentService));
    }

    /// <inheritdoc />
    public async Task<bool> RequestApprovalAsync(
        string workflowId,
        ApprovalConfig config,
        CancellationToken cancellationToken = default)
    {
        var request = new ApprovalRequestBuilder()
            .WithTitle(config.Name)
            .WithTimeout(config.Timeout ?? TimeSpan.FromHours(24))
            .WithCorrelationId(workflowId)
            .Build();

        var response = await _channel
            .RequestApprovalAsync(request, cancellationToken)
            .ConfigureAwait(false);

        return response.Approved;
    }

    /// <summary>
    /// Manually approves a pending workflow approval identified by <paramref name="workflowId"/>.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="PersistentApprovalService.DirectComplete"/> to immediately deliver an
    /// approved response without re-evaluating quorum. This mirrors the behaviour of the
    /// legacy <c>InMemoryApprovalService.Approve</c>.
    /// </remarks>
    /// <param name="workflowId">
    /// The workflow identifier used as the correlation ID when
    /// <see cref="RequestApprovalAsync"/> was called.
    /// </param>
    public void Approve(string workflowId)
    {
        var vote = new ApprovalRecord(
            ApproverId: "manual:scheduling",
            ApproverDisplayName: "Manual (Scheduling)",
            Approved: true,
            Comment: "Manually approved via scheduling adapter.",
            Timestamp: DateTimeOffset.UtcNow,
            Channel: "scheduling");

        _persistentService.DirectComplete(
            workflowId,
            ApprovalResponse.ApprovedBy(new[] { vote }));
    }

    /// <summary>
    /// Manually rejects a pending workflow approval identified by <paramref name="workflowId"/>.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="PersistentApprovalService.DirectComplete"/> to immediately deliver a
    /// rejected response without re-evaluating quorum. This mirrors the behaviour of the
    /// legacy <c>InMemoryApprovalService.Reject</c>.
    /// </remarks>
    /// <param name="workflowId">
    /// The workflow identifier used as the correlation ID when
    /// <see cref="RequestApprovalAsync"/> was called.
    /// </param>
    public void Reject(string workflowId)
    {
        var vote = new ApprovalRecord(
            ApproverId: "manual:scheduling",
            ApproverDisplayName: "Manual (Scheduling)",
            Approved: false,
            Comment: "Manually rejected via scheduling adapter.",
            Timestamp: DateTimeOffset.UtcNow,
            Channel: "scheduling");

        _persistentService.DirectComplete(
            workflowId,
            ApprovalResponse.Rejected("Manually rejected", new[] { vote }));
    }
}
