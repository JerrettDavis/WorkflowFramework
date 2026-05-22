using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Teams.Tests;

public sealed class TeamsCallbackTokenServiceTests
{
    private static TeamsCallbackTokenService MakeService(string secret = "supersecretkey12345678")
    {
        var options = Options.Create(new TeamsApprovalOptions { CallbackSharedSecret = secret });
        return new TeamsCallbackTokenService(options);
    }

    // ------------------------------------------------------------------
    // Round-trip
    // ------------------------------------------------------------------

    [Fact]
    public void CreateAndTryVerify_RoundTrip_ReturnsTrue()
    {
        var svc = MakeService();
        var expiry = DateTimeOffset.UtcNow.AddHours(1);
        var token = svc.Create("corr-123", true, expiry);

        var result = svc.TryVerify(token, out var correlationId, out var decision, out var expiresAt);

        result.Should().BeTrue();
        correlationId.Should().Be("corr-123");
        decision.Should().BeTrue();
        expiresAt.ToUnixTimeSeconds().Should().Be(expiry.ToUnixTimeSeconds());
    }

    [Fact]
    public void Create_DecisionFalse_PreservedInVerify()
    {
        var svc = MakeService();
        var token = svc.Create("corr-456", false, DateTimeOffset.UtcNow.AddHours(1));

        svc.TryVerify(token, out _, out var decision, out _).Should().BeTrue();
        decision.Should().BeFalse();
    }

    [Fact]
    public void Create_CorrelationId_PreservedInVerify()
    {
        var svc = MakeService();
        var token = svc.Create("my-unique-corr", true, DateTimeOffset.UtcNow.AddHours(1));

        svc.TryVerify(token, out var correlationId, out _, out _).Should().BeTrue();
        correlationId.Should().Be("my-unique-corr");
    }

    // ------------------------------------------------------------------
    // Tampering
    // ------------------------------------------------------------------

    [Fact]
    public void TryVerify_TamperedPayload_ReturnsFalse()
    {
        var svc = MakeService();
        var token = svc.Create("corr-789", true, DateTimeOffset.UtcNow.AddHours(1));

        // Tamper the first character of the payload section.
        var parts = token.Split('.');
        var tampered = "X" + parts[0][1..] + "." + parts[1];

        svc.TryVerify(tampered, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryVerify_WrongSecret_ReturnsFalse()
    {
        var svc1 = MakeService("secret-one-abcdefghij");
        var svc2 = MakeService("secret-two-abcdefghij");

        var token = svc1.Create("corr-abc", true, DateTimeOffset.UtcNow.AddHours(1));

        svc2.TryVerify(token, out _, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void TryVerify_ExpiredToken_ReturnsFalse()
    {
        var svc = MakeService();
        var token = svc.Create("corr-exp", true, DateTimeOffset.UtcNow.AddSeconds(-1));

        svc.TryVerify(token, out _, out _, out _).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Malformed input
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("notadottoken")]
    [InlineData(".onlysig")]
    [InlineData("payload.")]
    [InlineData("!!!.!!!")]
    public void TryVerify_Malformed_ReturnsFalseWithoutThrowing(string bad)
    {
        var svc = MakeService();
        var act = () => svc.TryVerify(bad, out _, out _, out _);
        act.Should().NotThrow();
        svc.TryVerify(bad, out _, out _, out _).Should().BeFalse();
    }
}
