using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Approvals;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Tests;

/// <summary>
/// TinyBDD-style characterization scenarios for <see cref="SingleChannelRouter"/>.
/// </summary>
public sealed class SingleChannelRouterTests
{
    // ── helpers ──────────────────────────────────────────────────────────

    private static IApprovalChannel MakeChannel(string name = "ch")
    {
        var ch = Substitute.For<IApprovalChannel>();
        ch.Name.Returns(name);
        return ch;
    }

    private static ApprovalRequest MakeRequest(string correlationId = "corr")
        => new ApprovalRequestBuilder()
            .WithTitle("Test")
            .WithCorrelationId(correlationId)
            .Build();

    // ── constructor guards ────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullChannel_Throws()
    {
        var act = () => new SingleChannelRouter(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("channel");
    }

    // ── resolution ────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_AlwaysReturnsTheSameChannel()
    {
        var channel = MakeChannel("email");
        var router = new SingleChannelRouter(channel);

        router.Resolve(MakeRequest("a")).Should().BeSameAs(channel);
        router.Resolve(MakeRequest("b")).Should().BeSameAs(channel);
    }

    [Fact]
    public void Resolve_IgnoresRequestContent()
    {
        var channel = MakeChannel("teams");
        var router = new SingleChannelRouter(channel);

        // context key asking for a different channel — ignored
        var request = new ApprovalRequestBuilder()
            .WithTitle("T")
            .WithCorrelationId("c")
            .WithContext("channel", "slack")
            .Build();

        router.Resolve(request).Should().BeSameAs(channel);
    }

    [Fact]
    public void Resolve_NullRequest_Throws()
    {
        var router = new SingleChannelRouter(MakeChannel());
        var act = () => router.Resolve(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("request");
    }
}
