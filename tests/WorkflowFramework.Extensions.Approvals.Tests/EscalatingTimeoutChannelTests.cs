using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Approvals;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Tests;

public sealed class EscalatingTimeoutChannelTests
{
    private static ApprovalRequest MakeRequest() =>
        new ApprovalRequestBuilder()
            .WithTitle("Deploy")
            .WithTimeout(TimeSpan.FromMinutes(5))
            .Build();

    // ------------------------------------------------------------------
    // Inner completes within timeout → as-is response
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_CompletesWithinTimeout_ReturnsInnerResponse()
    {
        var inner = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("inner");

        var approved = ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
        inner.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(approved);

        var channel = new EscalatingTimeoutChannel(inner, TimeSpan.FromSeconds(5), OnTimeoutAction.AutoReject);

        var result = await channel.RequestApprovalAsync(MakeRequest());
        result.Should().Be(approved);
    }

    // ------------------------------------------------------------------
    // AutoReject on timeout
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_AutoRejectOnTimeout_ReturnsRejected()
    {
        var inner = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("inner");

        inner.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var ct = ci.Arg<CancellationToken>();
                await Task.Delay(Timeout.Infinite, ct);
                return ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
            });

        var channel = new EscalatingTimeoutChannel(inner, TimeSpan.FromMilliseconds(50), OnTimeoutAction.AutoReject);

        var result = await channel.RequestApprovalAsync(MakeRequest());

        result.Approved.Should().BeFalse();
        result.Outcome.Should().Be(ApprovalOutcome.TimedOut);
    }

    // ------------------------------------------------------------------
    // AutoApprove on timeout
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_AutoApproveOnTimeout_ReturnsApproved()
    {
        var inner = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("inner");

        inner.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var ct = ci.Arg<CancellationToken>();
                await Task.Delay(Timeout.Infinite, ct);
                return ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
            });

        var channel = new EscalatingTimeoutChannel(inner, TimeSpan.FromMilliseconds(50), OnTimeoutAction.AutoApprove);

        var result = await channel.RequestApprovalAsync(MakeRequest());

        result.Approved.Should().BeTrue();
        result.Outcome.Should().Be(ApprovalOutcome.Approved);
    }

    // ------------------------------------------------------------------
    // Escalate with target → secondary called
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_EscalateWithTarget_CallsSecondary()
    {
        var inner = Substitute.For<IApprovalChannel>();
        var target = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("inner");
        target.Name.Returns("escalation");

        inner.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var ct = ci.Arg<CancellationToken>();
                await Task.Delay(Timeout.Infinite, ct);
                return ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
            });

        var escalationResponse = ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
        target.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(escalationResponse);

        var channel = new EscalatingTimeoutChannel(
            inner, TimeSpan.FromMilliseconds(50), OnTimeoutAction.Escalate, target);

        var result = await channel.RequestApprovalAsync(MakeRequest());
        result.Should().Be(escalationResponse);
        await target.Received(1).RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------
    // Escalate without target → falls back to AutoReject
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_EscalateWithoutTarget_FallsBackToAutoReject()
    {
        var inner = Substitute.For<IApprovalChannel>();
        inner.Name.Returns("inner");

        inner.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var ct = ci.Arg<CancellationToken>();
                await Task.Delay(Timeout.Infinite, ct);
                return ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
            });

        var channel = new EscalatingTimeoutChannel(
            inner, TimeSpan.FromMilliseconds(50), OnTimeoutAction.Escalate, escalationTarget: null);

        var result = await channel.RequestApprovalAsync(MakeRequest());

        result.Approved.Should().BeFalse();
        // Falls back to TimedOut (same as AutoReject path).
        result.Outcome.Should().Be(ApprovalOutcome.TimedOut);
    }
}
