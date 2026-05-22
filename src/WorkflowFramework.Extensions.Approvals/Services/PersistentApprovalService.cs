using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// A decorator over an inner <see cref="IApprovalChannel"/> that persists in-flight approval
/// requests to an <see cref="IApprovalStore"/> and wires up quorum evaluation so that external
/// vote submissions (e.g., from webhooks) can complete the in-flight task.
/// </summary>
/// <remarks>
/// <para>
/// For each in-flight request a <see cref="TaskCompletionSource{TResult}"/> is maintained.
/// The TCS is completed either by <see cref="ResolveExternalAsync"/> when quorum is reached, or
/// by a deadline timer that fires the configured <see cref="OnTimeoutAction"/>.
/// </para>
/// <para>
/// Per-correlation semaphores guarantee that concurrent vote submissions are serialised; quorum
/// is never evaluated on a stale snapshot.
/// </para>
/// </remarks>
public sealed class PersistentApprovalService : IApprovalChannel
{
    private readonly IApprovalChannel _inner;
    private readonly IApprovalStore _store;
    private readonly ILogger<PersistentApprovalService> _logger;

    // Keyed by CorrelationId.
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ApprovalResponse>> _pending =
        new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Initialises a new instance of <see cref="PersistentApprovalService"/>.
    /// </summary>
    /// <param name="inner">
    /// The underlying channel used to dispatch the request to approvers. Must not be
    /// <see langword="null"/>.
    /// </param>
    /// <param name="store">
    /// The store where pending approvals and votes are persisted. Must not be
    /// <see langword="null"/>.
    /// </param>
    /// <param name="logger">Optional structured logger. A no-op logger is used when <see langword="null"/>.</param>
    public PersistentApprovalService(
        IApprovalChannel inner,
        IApprovalStore store,
        ILogger<PersistentApprovalService>? logger = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? NullLogger<PersistentApprovalService>.Instance;
    }

    /// <inheritdoc />
    public string Name => "approvals:persistent";

    // -------------------------------------------------------------------------
    // IApprovalChannel
    // -------------------------------------------------------------------------

    /// <inheritdoc />
    /// <remarks>
    /// <list type="number">
    ///   <item>Validates the request (RequiredApprovers &gt;= 1, Timeout &gt; Zero, non-empty Title).</item>
    ///   <item>Persists a <see cref="PendingApproval"/> via <see cref="IApprovalStore.SaveAsync"/>.</item>
    ///   <item>Creates a TCS and a per-correlation semaphore for serialising votes.</item>
    ///   <item>Schedules a deadline timer that fires <see cref="OnTimeoutAction.AutoReject"/> by default.</item>
    ///   <item>Dispatches the request to the inner channel in a background task.</item>
    ///   <item>Awaits the TCS; cleans up and calls <see cref="IApprovalStore.CompleteAsync"/> on resolution.</item>
    /// </list>
    /// </remarks>
    public async Task<ApprovalResponse> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        ValidateRequest(request);

        var now = DateTimeOffset.UtcNow;
        var pending = new PendingApproval(
            CorrelationId: request.CorrelationId,
            Request: request,
            PrimaryChannel: _inner.Name,
            CreatedAt: now,
            DeadlineAt: now + request.Timeout,
            Votes: Array.Empty<ApprovalRecord>(),
            EscalationChannel: null,
            TimeoutAction: OnTimeoutAction.AutoReject);

        await _store.SaveAsync(pending, cancellationToken).ConfigureAwait(false);

        var tcs = new TaskCompletionSource<ApprovalResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[request.CorrelationId] = tcs;
        _semaphores[request.CorrelationId] = new SemaphoreSlim(1, 1);

        // Schedule deadline timer (not linked to caller's token — it must fire even if caller disconnects).
        var deadlineTimer = Task.Delay(request.Timeout, CancellationToken.None)
            .ContinueWith(async _ =>
            {
                await FireTimeoutAsync(request.CorrelationId, OnTimeoutAction.AutoReject, null)
                    .ConfigureAwait(false);
            }, TaskScheduler.Default);

        // Dispatch inner channel in background (it may block until an external resolver calls back).
        _ = Task.Run(async () =>
        {
            try
            {
                var response = await _inner
                    .RequestApprovalAsync(request, cancellationToken)
                    .ConfigureAwait(false);

                // Inner returned directly (e.g., synchronous CLI channel).
                if (_pending.TryGetValue(request.CorrelationId, out var innerTcs))
                    innerTcs.TrySetResult(response);
            }
            catch (OperationCanceledException)
            {
                if (_pending.TryGetValue(request.CorrelationId, out var innerTcs))
                    innerTcs.TrySetCanceled();
            }
            catch (Exception ex)
            {
                if (_pending.TryGetValue(request.CorrelationId, out var innerTcs))
                    innerTcs.TrySetException(ex);
            }
        }, cancellationToken);

        ApprovalResponse result;
        try
        {
            result = await tcs.Task.ConfigureAwait(false);
        }
        catch
        {
            Cleanup(request.CorrelationId);
            throw;
        }

        Cleanup(request.CorrelationId);

        try
        {
            await _store.CompleteAsync(request.CorrelationId, result, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to mark approval '{CorrelationId}' as complete in store.",
                request.CorrelationId);
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // External vote submission (called by webhook handlers, CLI adapters, etc.)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Submits an external vote for a pending approval and re-evaluates quorum.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is the integration point for all external systems (webhook controllers,
    /// CLI adapters, Slack action callbacks, etc.) that receive a human approver's decision
    /// and need to feed it back into the orchestrator.
    /// </para>
    /// <para>
    /// If <see cref="ApprovalRequest.AllowedRoles"/> is non-null and non-empty, only voters
    /// whose <see cref="ApprovalRecord.ApproverId"/> matches one of those role names are
    /// accepted. Others receive an <see cref="UnauthorizedAccessException"/>.
    /// </para>
    /// <para>
    /// Concurrent calls for the same <paramref name="correlationId"/> are serialised by a
    /// per-correlation <see cref="SemaphoreSlim"/>. Quorum is evaluated on the post-append
    /// snapshot returned by <see cref="IApprovalStore.AppendVoteAsync"/>.
    /// </para>
    /// </remarks>
    /// <param name="correlationId">The correlation ID of the pending approval to update.</param>
    /// <param name="vote">The vote to record.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the voter's ID does not appear in <see cref="ApprovalRequest.AllowedRoles"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no in-flight TCS exists for <paramref name="correlationId"/>. This can happen
    /// if the correlation has already been completed or was never started.
    /// </exception>
    public async Task ResolveExternalAsync(
        string correlationId,
        ApprovalRecord vote,
        CancellationToken cancellationToken = default)
    {
        if (!_semaphores.TryGetValue(correlationId, out var sem))
            throw new InvalidOperationException(
                $"No in-flight approval found for CorrelationId '{correlationId}'.");

        await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var updated = await _store.AppendVoteAsync(correlationId, vote, cancellationToken)
                .ConfigureAwait(false);

            var request = updated.Request;

            // Role check — only when AllowedRoles is non-null and non-empty.
            if (request.AllowedRoles is { Count: > 0 } roles)
            {
                var allowed = roles.Contains(vote.ApproverId, StringComparer.OrdinalIgnoreCase);
                if (!allowed)
                    throw new UnauthorizedAccessException(
                        $"Approver '{vote.ApproverId}' is not in the allowed roles list for approval '{correlationId}'.");
            }

            int totalAddressable = request.AllowedRoles is { Count: > 0 }
                ? request.AllowedRoles.Count
                : int.MaxValue / 2;

            // Cap totalAddressable to at least votes.Count to avoid argument exceptions.
            var votesSnapshot = updated.Votes;
            if (totalAddressable < votesSnapshot.Count)
                totalAddressable = votesSnapshot.Count;

            var outcome = QuorumApprovalAggregator.Evaluate(
                votesSnapshot,
                request.RequiredApprovers,
                totalAddressable);

            if (!_pending.TryGetValue(correlationId, out var tcs))
                return; // Already completed.

            switch (outcome)
            {
                case ApprovalOutcome.Approved:
                    tcs.TrySetResult(ApprovalResponse.ApprovedBy(votesSnapshot));
                    break;

                case ApprovalOutcome.Rejected:
                    tcs.TrySetResult(ApprovalResponse.Rejected("Quorum rejected", votesSnapshot));
                    break;

                // Pending — more votes needed; do nothing.
            }
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// Returns a <see cref="Task{ApprovalResponse}"/> that completes when the specified
    /// correlation reaches a terminal state.  Used by the rehydration hosted service to
    /// re-attach callers to already-started approvals after a process restart.
    /// </summary>
    /// <param name="correlationId">The correlation ID to wait on.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns>
    /// The <see cref="ApprovalResponse"/> once the request completes, or a cancelled task
    /// if <paramref name="cancellationToken"/> fires first.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no in-flight TCS exists for <paramref name="correlationId"/>.
    /// </exception>
    public async Task<ApprovalResponse> WaitForCompletionAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        if (!_pending.TryGetValue(correlationId, out var tcs))
            throw new InvalidOperationException(
                $"No in-flight approval found for CorrelationId '{correlationId}'.");

        using var reg = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
        return await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Directly completes an in-flight approval request with a pre-built
    /// <see cref="ApprovalResponse"/>, bypassing quorum evaluation.
    /// </summary>
    /// <remarks>
    /// Use this method when the caller has already determined the final outcome
    /// (e.g., the scheduling adapter's <c>Approve</c> / <c>Reject</c> convenience methods)
    /// and does not need quorum math applied.  If the correlation has already been completed,
    /// this call is a no-op.
    /// </remarks>
    /// <param name="correlationId">The correlation ID of the pending approval to terminate.</param>
    /// <param name="response">The terminal response to deliver.</param>
    public void DirectComplete(string correlationId, ApprovalResponse response)
    {
        if (_pending.TryGetValue(correlationId, out var tcs))
            tcs.TrySetResult(response);
    }

    /// <summary>
    /// Re-attaches an in-flight <see cref="PendingApproval"/> loaded from the store on
    /// process startup so that external vote submissions can resume completing it.
    /// Called by <see cref="ApprovalRehydrationHostedService"/>.
    /// </summary>
    /// <param name="pending">The pending approval to rehydrate.</param>
    public void Rehydrate(PendingApproval pending)
    {
        if (pending is null) throw new ArgumentNullException(nameof(pending));
        if (pending.IsComplete) return;

        // Register TCS and semaphore so external resolvers can find this correlation.
        _pending.TryAdd(pending.CorrelationId,
            new TaskCompletionSource<ApprovalResponse>(TaskCreationOptions.RunContinuationsAsynchronously));
        _semaphores.TryAdd(pending.CorrelationId, new SemaphoreSlim(1, 1));

        // Schedule remaining time until deadline.
        var remaining = pending.DeadlineAt - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            // Already past deadline — fire immediately.
            _ = Task.Run(() => FireTimeoutAsync(pending.CorrelationId, pending.TimeoutAction, null));
        }
        else
        {
            _ = Task.Delay(remaining, CancellationToken.None).ContinueWith(async _ =>
                await FireTimeoutAsync(pending.CorrelationId, pending.TimeoutAction, null)
                    .ConfigureAwait(false), TaskScheduler.Default);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task FireTimeoutAsync(
        string correlationId,
        OnTimeoutAction action,
        IReadOnlyList<ApprovalRecord>? existingVotes)
    {
        if (!_pending.TryGetValue(correlationId, out var tcs))
            return; // Already completed.

        IReadOnlyList<ApprovalRecord> votes = existingVotes ?? Array.Empty<ApprovalRecord>();

        // Try to get votes from store if not supplied.
        if (existingVotes is null)
        {
            try
            {
                var loaded = await _store.LoadAsync(correlationId, CancellationToken.None)
                    .ConfigureAwait(false);
                if (loaded is not null)
                    votes = loaded.Votes;
            }
            catch
            {
                // Best-effort — proceed with empty votes.
            }
        }

        ApprovalResponse response = action switch
        {
            OnTimeoutAction.AutoApprove => new ApprovalResponse(true, "Auto-approved on timeout", votes)
            {
                Outcome = ApprovalOutcome.Approved
            },
            _ => ApprovalResponse.TimedOut(votes)
        };

        tcs.TrySetResult(response);
    }

    private void Cleanup(string correlationId)
    {
        _pending.TryRemove(correlationId, out _);
        // Note: we intentionally do NOT dispose the semaphore here.
        // ResolveExternalAsync acquires the semaphore and calls Release() in a finally block.
        // If we disposed the semaphore while it was held by ResolveExternalAsync, the Release()
        // call would throw ObjectDisposedException. SemaphoreSlim wraps a AvailableWaitHandle
        // which is only allocated on first access via the WaitHandle property (not used here),
        // so skipping Dispose() is safe — the GC will reclaim it without leaking resources.
        _semaphores.TryRemove(correlationId, out _);
    }

    private static void ValidateRequest(ApprovalRequest request)
    {
        if (request.RequiredApprovers < 1)
            throw new ArgumentException(
                "RequiredApprovers must be >= 1.", nameof(request));

        if (request.Timeout <= TimeSpan.Zero)
            throw new ArgumentException(
                "Timeout must be a positive TimeSpan.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException(
                "Title must not be empty.", nameof(request));
    }
}
