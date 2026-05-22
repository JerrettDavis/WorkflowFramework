using FluentAssertions;
using Microsoft.Extensions.Options;
using WorkflowFramework.Extensions.Approvals.Email;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Email.Tests;

public sealed class ApprovalTokenServiceTests
{
    private static ApprovalTokenService CreateService(string? key = null)
    {
        key ??= ValidKey();
        var options = Options.Create(new EmailApprovalOptions
        {
            SmtpHost = "smtp.test",
            From = "test@test.com",
            ApproveUrlTemplate = "https://example.com/approve?t={token}",
            RejectUrlTemplate = "https://example.com/reject?t={token}",
            TokenSigningKey = key
        });
        return new ApprovalTokenService(options);
    }

    private static string ValidKey() =>
        // 32 bytes = 256-bit key; base64-encode it
        Convert.ToBase64String(new byte[32]
        {
            1, 2, 3, 4, 5, 6, 7, 8,
            9, 10, 11, 12, 13, 14, 15, 16,
            17, 18, 19, 20, 21, 22, 23, 24,
            25, 26, 27, 28, 29, 30, 31, 32
        });

    private static ApprovalTokenPayload ValidPayload(string correlationId = "abc123") =>
        new(
            CorrelationId: correlationId,
            ApproverId: "user@example.com",
            Decision: true,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1),
            ApproverDisplayName: "Test User");

    // ------------------------------------------------------------------
    // Round-trip
    // ------------------------------------------------------------------

    [Fact]
    public void Create_ThenTryParse_ReturnsOriginalPayload()
    {
        var service = CreateService();
        var payload = ValidPayload();

        var token = service.Create(payload);
        var parsed = service.TryParse(token, out var result);

        parsed.Should().BeTrue();
        result.CorrelationId.Should().Be(payload.CorrelationId);
        result.ApproverId.Should().Be(payload.ApproverId);
        result.Decision.Should().Be(payload.Decision);
        result.ApproverDisplayName.Should().Be(payload.ApproverDisplayName);
        result.ExpiresAt.Should().BeCloseTo(payload.ExpiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_ThenTryParse_RejectDecision_RoundTrips()
    {
        var service = CreateService();
        var payload = new ApprovalTokenPayload("id1", "user@x.com", false, DateTimeOffset.UtcNow.AddHours(1));

        var token = service.Create(payload);
        service.TryParse(token, out var result).Should().BeTrue();
        result.Decision.Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Tampered JSON portion
    // ------------------------------------------------------------------

    [Fact]
    public void TryParse_TamperedJsonPortion_ReturnsFalse()
    {
        var service = CreateService();
        var token = service.Create(ValidPayload());

        var parts = token.Split('.');
        // Mutate the JSON portion by appending a char.
        var tampered = parts[0] + "X" + "." + parts[1];

        service.TryParse(tampered, out _).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Tampered HMAC portion
    // ------------------------------------------------------------------

    [Fact]
    public void TryParse_TamperedHmacPortion_ReturnsFalse()
    {
        var service = CreateService();
        var token = service.Create(ValidPayload());

        var parts = token.Split('.');
        var tampered = parts[0] + "." + parts[1] + "X";

        service.TryParse(tampered, out _).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Wrong signing key
    // ------------------------------------------------------------------

    [Fact]
    public void TryParse_WrongSigningKey_ReturnsFalse()
    {
        var service1 = CreateService();
        var differentKey = Convert.ToBase64String(new byte[32]
        {
            255, 254, 253, 252, 251, 250, 249, 248,
            247, 246, 245, 244, 243, 242, 241, 240,
            239, 238, 237, 236, 235, 234, 233, 232,
            231, 230, 229, 228, 227, 226, 225, 224
        });
        var service2 = CreateService(differentKey);

        var token = service1.Create(ValidPayload());

        service2.TryParse(token, out _).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Expired token
    // ------------------------------------------------------------------

    [Fact]
    public void TryParse_ExpiredToken_ReturnsFalse()
    {
        var service = CreateService();
        var expired = new ApprovalTokenPayload(
            "corr1", "user@x.com", true,
            ExpiresAt: DateTimeOffset.UtcNow.AddSeconds(-1));

        var token = service.Create(expired);

        service.TryParse(token, out _).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // Malformed tokens
    // ------------------------------------------------------------------

    [Fact]
    public void TryParse_NoDot_ReturnsFalse()
    {
        var service = CreateService();
        service.TryParse("nodotintoken", out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_EmptyString_ReturnsFalse()
    {
        var service = CreateService();
        service.TryParse(string.Empty, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_Null_ReturnsFalse_DoesNotThrow()
    {
        var service = CreateService();
        var act = () => service.TryParse(null, out _);
        act.Should().NotThrow();
        service.TryParse(null, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_MalformedBase64_ReturnsFalse()
    {
        var service = CreateService();
        service.TryParse("!!!.###", out _).Should().BeFalse();
    }

    // ------------------------------------------------------------------
    // HMAC uses constant-time compare (malformed HMAC does not throw)
    // ------------------------------------------------------------------

    [Fact]
    public void TryParse_InvalidHmacPart_DoesNotThrow()
    {
        var service = CreateService();
        var token = service.Create(ValidPayload());
        var parts = token.Split('.');

        // Replace HMAC with a valid-looking but wrong base64url value.
        var badToken = parts[0] + ".AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

        var act = () => service.TryParse(badToken, out _);
        act.Should().NotThrow();
    }
}
