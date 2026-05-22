using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Approvals;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Tests;

public sealed class CompositeApprovalChannelTests
{
    private static ApprovalRequest MakeRequest() =>
        new ApprovalRequestBuilder()
            .WithTitle("Deploy")
            .WithTimeout(TimeSpan.FromMinutes(5))
            .Build();

    // ------------------------------------------------------------------
    // Primary returns terminal (non-timeout) response → pass through
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_PrimaryApproved_ReturnsPrimaryResponse_SecondaryNotCalled()
    {
        var primary = Substitute.For<IApprovalChannel>();
        var secondary = Substitute.For<IApprovalChannel>();
        primary.Name.Returns("primary");
        secondary.Name.Returns("secondary");

        var approved = ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
        primary.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(approved);

        var composite = new CompositeApprovalChannel(primary, TimeSpan.FromSeconds(5), secondary);

        var result = await composite.RequestApprovalAsync(MakeRequest());

        result.Should().Be(approved);
        await secondary.DidNotReceive().RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestApprovalAsync_PrimaryRejected_ReturnsPrimaryResponse_SecondaryNotCalled()
    {
        var primary = Substitute.For<IApprovalChannel>();
        var secondary = Substitute.For<IApprovalChannel>();
        primary.Name.Returns("primary");
        secondary.Name.Returns("secondary");

        var rejected = ApprovalResponse.Rejected("no", Array.Empty<ApprovalRecord>());
        primary.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(rejected);

        var composite = new CompositeApprovalChannel(primary, TimeSpan.FromSeconds(5), secondary);

        var result = await composite.RequestApprovalAsync(MakeRequest());

        result.Should().Be(rejected);
        await secondary.DidNotReceive().RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------
    // Primary exceeds escalateAfter → secondary called with same CorrelationId
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_PrimaryTimesOut_SecondaryCalledWithSameCorrelation()
    {
        var primary = Substitute.For<IApprovalChannel>();
        var secondary = Substitute.For<IApprovalChannel>();
        primary.Name.Returns("primary");
        secondary.Name.Returns("secondary");

        // Primary hangs until cancellation.
        primary.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var ct = ci.Arg<CancellationToken>();
                await Task.Delay(Timeout.Infinite, ct);
                return ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>()); // never reached
            });

        var secondaryResponse = ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
        ApprovalRequest? capturedRequest = null;
        secondary.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedRequest = ci.Arg<ApprovalRequest>();
                return Task.FromResult(secondaryResponse);
            });

        var request = MakeRequest();
        var composite = new CompositeApprovalChannel(primary, TimeSpan.FromMilliseconds(50), secondary);

        var result = await composite.RequestApprovalAsync(request);

        result.Outcome.Should().Be(ApprovalOutcome.Approved);
        capturedRequest.Should().NotBeNull();
        capturedRequest!.CorrelationId.Should().Be(request.CorrelationId);

        // Result should include escalation sentinel vote.
        result.Approvals.Should().Contain(v => v.ApproverId == "system:escalated");
    }

    // ------------------------------------------------------------------
    // Primary throws non-cancellation exception → propagates
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_PrimaryThrowsNonCancel_Propagates()
    {
        var primary = Substitute.For<IApprovalChannel>();
        var secondary = Substitute.For<IApprovalChannel>();
        primary.Name.Returns("primary");
        secondary.Name.Returns("secondary");

        primary.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns<ApprovalResponse>(_ => throw new InvalidOperationException("boom"));

        var composite = new CompositeApprovalChannel(primary, TimeSpan.FromSeconds(5), secondary);

        var act = async () => await composite.RequestApprovalAsync(MakeRequest());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    // ------------------------------------------------------------------
    // Caller cancellation propagates (not treated as escalation)
    // ------------------------------------------------------------------

    [Fact]
    public async Task RequestApprovalAsync_CallerCancelled_Throws_DoesNotEscalate()
    {
        var primary = Substitute.For<IApprovalChannel>();
        var secondary = Substitute.For<IApprovalChannel>();
        primary.Name.Returns("primary");
        secondary.Name.Returns("secondary");

        primary.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var ct = ci.Arg<CancellationToken>();
                await Task.Delay(Timeout.Infinite, ct);
                return ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
            });

        using var cts = new CancellationTokenSource();
        var composite = new CompositeApprovalChannel(primary, TimeSpan.FromSeconds(30), secondary);

        var task = composite.RequestApprovalAsync(MakeRequest(), cts.Token);
        await Task.Delay(30); // let it start
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        await secondary.DidNotReceive().RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>());
    }
}
