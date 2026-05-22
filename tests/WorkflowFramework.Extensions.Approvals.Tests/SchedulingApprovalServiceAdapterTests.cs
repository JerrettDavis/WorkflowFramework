using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Approvals;
using WorkflowFramework.Extensions.Scheduling;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Tests;

public sealed class SchedulingApprovalServiceAdapterTests
{
    private static (SchedulingApprovalServiceAdapter adapter, PersistentApprovalService svc) Build(
        Func<ApprovalRequest, CancellationToken, Task<ApprovalResponse>>? channelBehavior = null)
    {
        var store = new InMemoryApprovalStore();
        var inner = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("test");

        if (channelBehavior is not null)
        {
            inner.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
                .Returns(ci => channelBehavior(ci.Arg<ApprovalRequest>(), ci.Arg<CancellationToken>()));
        }
        else
        {
            inner.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
                .Returns(async ci =>
                {
                    var ct = ci.Arg<CancellationToken>();
                    await Task.Delay(Timeout.Infinite, ct);
                    return ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
                });
        }

        var persistent = new PersistentApprovalService(inner, store);
        var adapter = new SchedulingApprovalServiceAdapter(persistent, persistent);
        return (adapter, persistent);
    }

    // ------------------------------------------------------------------
    // RequestApprovalAsync translates config and returns bool
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_Approved_ReturnsTrue()
    {
        var (adapter, svc) = Build();
        var config = new ApprovalConfig { Name = "Deploy", Timeout = TimeSpan.FromSeconds(5) };

        var task = adapter.RequestApprovalAsync("workflow-1", config);
        await Task.Delay(30);

        await svc.ResolveExternalAsync("workflow-1",
            new ApprovalRecord("u1", "u1", true, null, DateTimeOffset.UtcNow, "test"));

        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RequestApprovalAsync_Rejected_ReturnsFalse()
    {
        var (adapter, svc) = Build();
        var config = new ApprovalConfig { Name = "Deploy", Timeout = TimeSpan.FromSeconds(5) };

        var task = adapter.RequestApprovalAsync("workflow-2", config);
        await Task.Delay(30);

        // Use DirectComplete to reject — quorum math with unlimited voters cannot short-circuit.
        svc.DirectComplete("workflow-2",
            ApprovalResponse.Rejected("rejected", Array.Empty<ApprovalRecord>()));

        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));
        result.Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Approve() calls ResolveExternalAsync with approved vote
    // ------------------------------------------------------------------

    [Fact]
    public async Task Approve_CallsResolveExternal_WithApprovedVote()
    {
        var (adapter, svc) = Build();
        var config = new ApprovalConfig { Name = "Deploy", Timeout = TimeSpan.FromSeconds(5) };

        var task = adapter.RequestApprovalAsync("workflow-3", config);
        await Task.Delay(50); // let it register TCS

        adapter.Approve("workflow-3");

        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));
        result.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // Reject() calls ResolveExternalAsync with rejected vote
    // ------------------------------------------------------------------

    [Fact]
    public async Task Reject_CallsResolveExternal_WithRejectedVote()
    {
        var (adapter, svc) = Build();
        var config = new ApprovalConfig { Name = "Deploy", Timeout = TimeSpan.FromSeconds(5) };

        var task = adapter.RequestApprovalAsync("workflow-4", config);
        await Task.Delay(50);

        adapter.Reject("workflow-4");

        var result = await task.WaitAsync(TimeSpan.FromSeconds(3));
        result.Should().BeFalse();
    }
}
