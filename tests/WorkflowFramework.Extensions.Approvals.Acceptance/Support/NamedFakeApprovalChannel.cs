using WorkflowFramework.Extensions.Approvals;

namespace WorkflowFramework.Extensions.Approvals.Acceptance.Support;

/// <summary>
/// Wraps a FakeApprovalChannel with a specific name for escalation channel identification.
/// </summary>
public sealed class NamedFakeApprovalChannel : IApprovalChannel
{
    private readonly FakeApprovalChannel _inner;

    public NamedFakeApprovalChannel(string name, FakeApprovalChannel inner)
    {
        Name = name;
        _inner = inner;
    }

    public string Name { get; }

    public Task<ApprovalResponse> RequestApprovalAsync(
        ApprovalRequest request,
        CancellationToken cancellationToken = default)
        => _inner.RequestApprovalAsync(request, cancellationToken);
}
