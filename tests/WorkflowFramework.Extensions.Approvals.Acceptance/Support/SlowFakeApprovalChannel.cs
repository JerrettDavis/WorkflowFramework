using WorkflowFramework.Extensions.Approvals;

namespace WorkflowFramework.Extensions.Approvals.Acceptance.Support;

/// <summary>
/// A fake IApprovalChannel that never responds within any timeout.
/// Used for escalation and timeout scenarios where the primary must time out.
/// </summary>
public sealed class SlowFakeApprovalChannel : IApprovalChannel
{
    private readonly string _name;

    public SlowFakeApprovalChannel(string name = "slow-fake")
    {
        _name = name;
    }

    public string Name => _name;

    public async Task<ApprovalResponse> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        // Block indefinitely until cancellation.
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);

        // Unreachable — only here to satisfy the compiler.
        return ApprovalResponse.TimedOut(Array.Empty<ApprovalRecord>());
    }
}
