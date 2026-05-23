using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Approvals;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Tests;

/// <summary>
/// TinyBDD-style characterization scenarios for <see cref="MultiChannelApprovalService"/>.
/// </summary>
public sealed class MultiChannelApprovalServiceTests
{
    // ── helpers ──────────────────────────────────────────────────────────

    private static ApprovalRequest MakeRequest(string correlationId = "corr-1")
        => new ApprovalRequestBuilder()
            .WithTitle("Test approval")
            .WithCorrelationId(correlationId)
            .Build();

    // ── constructor guards ────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullPipeline_Throws()
    {
        var act = () => new MultiChannelApprovalService(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("pipeline");
    }

    // ── Name property ─────────────────────────────────────────────────────

    [Fact]
    public void Name_IsAlwaysApprovals()
    {
        var pipeline = Substitute.For<IApprovalChannel>();
        var svc = new MultiChannelApprovalService(pipeline);
        svc.Name.Should().Be("approvals");
    }

    // ── RequestApprovalAsync ──────────────────────────────────────────────

    [Fact]
    public async Task RequestApprovalAsync_DelegatesToPipeline()
    {
        var pipeline = Substitute.For<IApprovalChannel>();
        var expected = ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>());
        pipeline.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var svc = new MultiChannelApprovalService(pipeline);
        var request = MakeRequest();

        var result = await svc.RequestApprovalAsync(request);

        result.Should().Be(expected);
        await pipeline.Received(1).RequestApprovalAsync(request, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestApprovalAsync_PassesCancellationToken()
    {
        CancellationToken capturedToken = default;
        var pipeline = Substitute.For<IApprovalChannel>();
        pipeline.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedToken = ci.Arg<CancellationToken>();
                return Task.FromResult(ApprovalResponse.ApprovedBy(Array.Empty<ApprovalRecord>()));
            });

        using var cts = new CancellationTokenSource();
        var svc = new MultiChannelApprovalService(pipeline);

        await svc.RequestApprovalAsync(MakeRequest(), cts.Token);

        capturedToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task RequestApprovalAsync_PipelineRejectsRequest_ReturnsRejection()
    {
        var pipeline = Substitute.For<IApprovalChannel>();
        var rejected = ApprovalResponse.Rejected("No approvers available", Array.Empty<ApprovalRecord>());
        pipeline.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns(rejected);

        var svc = new MultiChannelApprovalService(pipeline);
        var result = await svc.RequestApprovalAsync(MakeRequest());

        result.Approved.Should().BeFalse();
        result.Reason.Should().Be("No approvers available");
    }

    [Fact]
    public async Task RequestApprovalAsync_PipelineThrows_PropagatesException()
    {
        var pipeline = Substitute.For<IApprovalChannel>();
        pipeline.RequestApprovalAsync(Arg.Any<ApprovalRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<ApprovalResponse>>(_ => throw new InvalidOperationException("pipeline down"));

        var svc = new MultiChannelApprovalService(pipeline);
        var act = () => svc.RequestApprovalAsync(MakeRequest());

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("pipeline down");
    }
}
