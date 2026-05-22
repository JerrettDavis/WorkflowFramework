using System.Collections.Concurrent;
using WorkflowFramework.Extensions.Approvals;

namespace WorkflowFramework.Extensions.Approvals.Acceptance.Support;

/// <summary>
/// A controllable fake IApprovalChannel for acceptance tests.
/// Tests call Complete(correlationId, response) to drive the channel to a terminal state.
/// </summary>
public sealed class FakeApprovalChannel : IApprovalChannel
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ApprovalResponse>> _pending =
        new(StringComparer.Ordinal);

    public string Name => "fake";

    public Task<ApprovalResponse> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<ApprovalResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[request.CorrelationId] = tcs;

        cancellationToken.Register(() =>
        {
            if (_pending.TryRemove(request.CorrelationId, out var t))
                t.TrySetCanceled(cancellationToken);
        });

        return tcs.Task;
    }

    /// <summary>
    /// Drives the in-flight request for <paramref name="correlationId"/> to the given response.
    /// </summary>
    public void Complete(string correlationId, ApprovalResponse response)
    {
        if (_pending.TryRemove(correlationId, out var tcs))
            tcs.TrySetResult(response);
    }

    /// <summary>
    /// Returns true when a TCS is registered for the given correlationId
    /// (i.e., RequestApprovalAsync was called but not yet completed).
    /// </summary>
    public bool HasPending(string correlationId) => _pending.ContainsKey(correlationId);
}
