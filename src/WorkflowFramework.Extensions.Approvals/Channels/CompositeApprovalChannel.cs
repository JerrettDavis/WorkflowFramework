using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// An <see cref="IApprovalChannel"/> that attempts a primary channel first and automatically
/// escalates to a secondary channel if the primary exceeds a configurable time window.
/// </summary>
/// <remarks>
/// <para>
/// The escalation is triggered when the primary channel takes longer than
/// <c>escalateAfter</c>. A <c>system:escalated</c> sentinel vote is prepended to the
/// secondary channel's response to record that escalation occurred.
/// </para>
/// <para>
/// Caller cancellation (i.e., the outer <see cref="CancellationToken"/> is cancelled)
/// is always honoured and re-thrown — it is never confused with the internal escalation
/// deadline.
/// </para>
/// </remarks>
public sealed class CompositeApprovalChannel : IApprovalChannel
{
    private readonly IApprovalChannel _primary;
    private readonly TimeSpan _escalateAfter;
    private readonly IApprovalChannel _secondary;
    private readonly ILogger<CompositeApprovalChannel> _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="CompositeApprovalChannel"/>.
    /// </summary>
    /// <param name="primary">The channel to attempt first. Must not be <see langword="null"/>.</param>
    /// <param name="escalateAfter">
    /// The time to wait for the primary channel before escalating to <paramref name="secondary"/>.
    /// Must be positive.
    /// </param>
    /// <param name="secondary">The fallback channel. Must not be <see langword="null"/>.</param>
    /// <param name="logger">
    /// Optional logger. When <see langword="null"/>, a no-op logger is used.
    /// </param>
    public CompositeApprovalChannel(
        IApprovalChannel primary,
        TimeSpan escalateAfter,
        IApprovalChannel secondary,
        ILogger<CompositeApprovalChannel>? logger = null)
    {
        _primary = primary ?? throw new ArgumentNullException(nameof(primary));
        _escalateAfter = escalateAfter;
        _secondary = secondary ?? throw new ArgumentNullException(nameof(secondary));
        _logger = logger ?? NullLogger<CompositeApprovalChannel>.Instance;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Returns <c>"composite({primary.Name}->{secondary.Name})"</c>.
    /// </remarks>
    public string Name => $"composite({_primary.Name}->{_secondary.Name})";

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Attempts <c>primary</c> with a linked cancellation source that fires after
    /// <c>escalateAfter</c>. When the primary returns a non-timeout/non-escalated response,
    /// it is returned as-is.
    /// </para>
    /// <para>
    /// When the escalation deadline fires and the caller's token is not also cancelled,
    /// the request is forwarded to the secondary channel using the original
    /// <see cref="ApprovalRequest.CorrelationId"/>. A sentinel
    /// <see cref="ApprovalRecord"/> (<c>ApproverId = "system:escalated"</c>) is prepended
    /// to the secondary response's vote list.
    /// </para>
    /// </remarks>
    public async Task<ApprovalResponse> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        using var deadlineCts = new CancellationTokenSource(_escalateAfter);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, deadlineCts.Token);

        try
        {
            var response = await _primary
                .RequestApprovalAsync(request, linkedCts.Token)
                .ConfigureAwait(false);

            // If primary returned normally (not timed out / escalated), pass through.
            if (response.Outcome != ApprovalOutcome.TimedOut &&
                response.Outcome != ApprovalOutcome.Escalated)
            {
                return response;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller cancelled — honour and propagate.
            throw;
        }
        catch (OperationCanceledException)
        {
            // Escalation deadline fired — fall through to secondary.
            _logger.LogInformation(
                "Approval '{CorrelationId}' escalated from '{Primary}' to '{Secondary}' after {Timeout}.",
                request.CorrelationId, _primary.Name, _secondary.Name, _escalateAfter);
        }

        // Escalate to secondary with same CorrelationId.
        var secondaryResponse = await _secondary
            .RequestApprovalAsync(request, cancellationToken)
            .ConfigureAwait(false);

        // Prepend sentinel escalation record.
        var escalationRecord = new ApprovalRecord(
            ApproverId: "system:escalated",
            ApproverDisplayName: "System (Escalation)",
            Approved: false,
            Comment: $"escalated from {_primary.Name}",
            Timestamp: DateTimeOffset.UtcNow,
            Channel: _primary.Name);

        var mergedVotes = new List<ApprovalRecord> { escalationRecord };
        mergedVotes.AddRange(secondaryResponse.Approvals);

        return secondaryResponse with { Approvals = mergedVotes.AsReadOnly() };
    }
}
