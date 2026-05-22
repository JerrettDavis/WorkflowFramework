using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// An <see cref="IApprovalChannel"/> decorator that enforces a timeout and applies a
/// configurable <see cref="OnTimeoutAction"/> when the inner channel does not respond within
/// the deadline.
/// </summary>
/// <remarks>
/// Unlike <see cref="CompositeApprovalChannel"/> (which always delegates to a secondary channel),
/// this class selects a terminal action based on the <see cref="OnTimeoutAction"/> value:
/// <list type="bullet">
///   <item><see cref="OnTimeoutAction.AutoReject"/> — returns a <see cref="ApprovalResponse.Rejected"/> response.</item>
///   <item><see cref="OnTimeoutAction.AutoApprove"/> — returns an approved response.</item>
///   <item><see cref="OnTimeoutAction.Escalate"/> — delegates to <c>escalationTarget</c> if
///   provided, otherwise falls back to <see cref="OnTimeoutAction.AutoReject"/>.</item>
/// </list>
/// </remarks>
public sealed class EscalatingTimeoutChannel : IApprovalChannel
{
    private readonly IApprovalChannel _inner;
    private readonly TimeSpan _timeout;
    private readonly OnTimeoutAction _onTimeout;
    private readonly IApprovalChannel? _escalationTarget;
    private readonly ILogger _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="EscalatingTimeoutChannel"/>.
    /// </summary>
    /// <param name="inner">The channel to attempt. Must not be <see langword="null"/>.</param>
    /// <param name="timeout">The time to wait before applying <paramref name="onTimeout"/>.</param>
    /// <param name="onTimeout">The action to apply when the deadline is reached.</param>
    /// <param name="escalationTarget">
    /// The channel to use when <paramref name="onTimeout"/> is <see cref="OnTimeoutAction.Escalate"/>.
    /// May be <see langword="null"/>; if so and escalation is requested, falls back to AutoReject.
    /// </param>
    /// <param name="logger">Optional logger. A no-op logger is used when <see langword="null"/>.</param>
    public EscalatingTimeoutChannel(
        IApprovalChannel inner,
        TimeSpan timeout,
        OnTimeoutAction onTimeout,
        IApprovalChannel? escalationTarget = null,
        ILogger? logger = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _timeout = timeout;
        _onTimeout = onTimeout;
        _escalationTarget = escalationTarget;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc />
    public string Name => _inner.Name;

    /// <inheritdoc />
    public async Task<ApprovalResponse> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        using var deadlineCts = new CancellationTokenSource(_timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, deadlineCts.Token);

        try
        {
            return await _inner
                .RequestApprovalAsync(request, linkedCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller cancelled — propagate.
            throw;
        }
        catch (OperationCanceledException)
        {
            // Deadline fired — apply configured action.
            _logger.LogInformation(
                "Approval '{CorrelationId}' timed out after {Timeout} on channel '{Channel}'. Action: {Action}.",
                request.CorrelationId, _timeout, _inner.Name, _onTimeout);
        }

        // Build partial vote list from store if possible; otherwise empty.
        IReadOnlyList<ApprovalRecord> partial = Array.Empty<ApprovalRecord>();

        return _onTimeout switch
        {
            OnTimeoutAction.AutoApprove => new ApprovalResponse(true, "Auto-approved on timeout", partial)
            {
                Outcome = ApprovalOutcome.Approved
            },
            OnTimeoutAction.Escalate when _escalationTarget is not null =>
                await _escalationTarget
                    .RequestApprovalAsync(request, cancellationToken)
                    .ConfigureAwait(false),
            _ => ApprovalResponse.TimedOut(partial)
        };
    }
}
