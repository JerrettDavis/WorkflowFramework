using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Approvals;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Tests;

/// <summary>
/// TinyBDD-style characterization scenarios for <see cref="NamedChannelRouter"/>.
/// </summary>
public sealed class NamedChannelRouterTests
{
    // ── helpers ──────────────────────────────────────────────────────────

    private static IApprovalChannel MakeChannel(string name)
    {
        var ch = Substitute.For<IApprovalChannel>();
        ch.Name.Returns(name);
        return ch;
    }

    private static ApprovalRequest MakeRequest(string? channelContext = null)
    {
        var builder = new ApprovalRequestBuilder()
            .WithTitle("Test")
            .WithCorrelationId("corr-1");

        if (channelContext != null)
            builder = builder.WithContext("channel", channelContext);

        return builder.Build();
    }

    // ── constructor guards ────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullChannels_Throws()
    {
        var act = () => new NamedChannelRouter(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("channels");
    }

    [Fact]
    public void Constructor_EmptyChannels_Throws()
    {
        var act = () => new NamedChannelRouter(Array.Empty<IApprovalChannel>());
        act.Should().Throw<ArgumentException>().WithParameterName("channels");
    }

    // ── resolution ────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_RequestWithMatchingChannelContext_ReturnsThatChannel()
    {
        var slack = MakeChannel("slack");
        var email = MakeChannel("email");
        var router = new NamedChannelRouter(new[] { slack, email });

        var request = MakeRequest("email");
        var resolved = router.Resolve(request);

        resolved.Should().BeSameAs(email);
    }

    [Fact]
    public void Resolve_MatchIsCaseInsensitive()
    {
        var teams = MakeChannel("teams");
        var router = new NamedChannelRouter(new[] { teams });

        // TEAMS vs teams — should match
        var resolved = router.Resolve(MakeRequest("TEAMS"));

        resolved.Should().BeSameAs(teams);
    }

    [Fact]
    public void Resolve_NoMatchingChannel_FallsBackToFirstChannel()
    {
        var first = MakeChannel("first");
        var second = MakeChannel("second");
        var router = new NamedChannelRouter(new[] { first, second });

        // request asks for "unknown" channel
        var resolved = router.Resolve(MakeRequest("unknown"));

        resolved.Should().BeSameAs(first);
    }

    [Fact]
    public void Resolve_NoChannelContextKey_FallsBackToFirstChannel()
    {
        var first = MakeChannel("first");
        var router = new NamedChannelRouter(new[] { first });

        // no "channel" key in context
        var resolved = router.Resolve(MakeRequest());

        resolved.Should().BeSameAs(first);
    }

    [Fact]
    public void Resolve_NullRequest_Throws()
    {
        var router = new NamedChannelRouter(new[] { MakeChannel("ch") });
        var act = () => router.Resolve(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("request");
    }

    [Fact]
    public void Resolve_SingleChannel_AlwaysReturnsThatChannel()
    {
        var only = MakeChannel("only");
        var router = new NamedChannelRouter(new[] { only });

        // no context, should still return the single channel
        router.Resolve(MakeRequest()).Should().BeSameAs(only);
        router.Resolve(MakeRequest("something-else")).Should().BeSameAs(only);
    }

    [Fact]
    public void Resolve_WhitespaceChannelContextValue_FallsBackToFirstChannel()
    {
        var first = MakeChannel("first");
        var router = new NamedChannelRouter(new[] { first });

        var builder = new ApprovalRequestBuilder()
            .WithTitle("T")
            .WithCorrelationId("c")
            .WithContext("channel", "   "); // whitespace-only

        var resolved = router.Resolve(builder.Build());
        resolved.Should().BeSameAs(first);
    }
}
