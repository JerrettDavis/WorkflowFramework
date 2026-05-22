using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using WorkflowFramework.Extensions.Approvals.Slack;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Slack.Tests;

public sealed class SlackSignatureValidatorTests
{
    private const string TestSecret = "8f742231b10e8888abcd99baed85671";
    private static readonly DateTimeOffset FixedNow = new(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static SlackSignatureValidator MakeValidator(
        string signingSecret = TestSecret,
        int maxAgeSeconds = 300) =>
        new(Options.Create(new SlackApprovalOptions
        {
            BotToken = "xoxb-test",
            ChannelId = "C123",
            SigningSecret = signingSecret,
            RequestSignatureMaxAgeSeconds = maxAgeSeconds
        }));

    private static (string timestamp, string signature) ComputeSignature(
        string rawBody,
        string secret,
        DateTimeOffset? time = null)
    {
        var t = time ?? FixedNow;
        var ts = t.ToUnixTimeSeconds().ToString();
        var sigBase = $"v0:{ts}:{rawBody}";
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var msgBytes = Encoding.UTF8.GetBytes(sigBase);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(msgBytes);
        var sig = "v0=" + Convert.ToHexString(hash).ToLowerInvariant();
        return (ts, sig);
    }

    [Fact]
    public void Validate_ValidSignatureAndCurrentTimestamp_ReturnsTrue()
    {
        var validator = MakeValidator();
        var body = "payload=hello%20world";
        var (ts, sig) = ComputeSignature(body, TestSecret, FixedNow);

        validator.Validate(ts, body, sig, FixedNow).Should().BeTrue();
    }

    [Fact]
    public void Validate_WrongSecret_ReturnsFalse()
    {
        var validator = MakeValidator();
        var body = "payload=hello";
        var (ts, sig) = ComputeSignature(body, "wrongsecret", FixedNow);

        validator.Validate(ts, body, sig, FixedNow).Should().BeFalse();
    }

    [Fact]
    public void Validate_ExpiredTimestamp_ReturnsFalse()
    {
        var validator = MakeValidator();
        var body = "payload=data";
        var oldTime = FixedNow.AddSeconds(-400); // older than maxAge 300
        var (ts, sig) = ComputeSignature(body, TestSecret, oldTime);

        validator.Validate(ts, body, sig, FixedNow).Should().BeFalse();
    }

    [Fact]
    public void Validate_FutureTimestamp_ReturnsFalse()
    {
        var validator = MakeValidator();
        var body = "payload=data";
        var futureTime = FixedNow.AddSeconds(400); // more than maxAge 300 in the future
        var (ts, sig) = ComputeSignature(body, TestSecret, futureTime);

        validator.Validate(ts, body, sig, FixedNow).Should().BeFalse();
    }

    [Fact]
    public void Validate_TamperedBody_ReturnsFalse()
    {
        var validator = MakeValidator();
        var (ts, sig) = ComputeSignature("original body", TestSecret, FixedNow);

        validator.Validate(ts, "tampered body", sig, FixedNow).Should().BeFalse();
    }

    [Fact]
    public void Validate_TamperedSignature_ReturnsFalse()
    {
        var validator = MakeValidator();
        var body = "payload=data";
        var (ts, _) = ComputeSignature(body, TestSecret, FixedNow);

        validator.Validate(ts, body, "v0=deadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeefdeadbeef", FixedNow)
            .Should().BeFalse();
    }

    [Fact]
    public void Validate_MissingV0Prefix_ReturnsFalse()
    {
        var validator = MakeValidator();
        var body = "payload=data";
        var (ts, sig) = ComputeSignature(body, TestSecret, FixedNow);
        var noPrefix = sig[3..]; // strip "v0="

        validator.Validate(ts, body, noPrefix, FixedNow).Should().BeFalse();
    }

    [Fact]
    public void Validate_MalformedHex_ReturnsFalse()
    {
        var validator = MakeValidator();
        var body = "payload=data";
        var (ts, _) = ComputeSignature(body, TestSecret, FixedNow);

        validator.Validate(ts, body, "v0=zzzzzzzzzz", FixedNow).Should().BeFalse();
    }

    [Fact]
    public void Validate_NullTimestamp_ReturnsFalse()
    {
        var validator = MakeValidator();
        validator.Validate(null!, "body", "v0=abc", FixedNow).Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyTimestamp_ReturnsFalse()
    {
        var validator = MakeValidator();
        validator.Validate(string.Empty, "body", "v0=abc", FixedNow).Should().BeFalse();
    }

    [Fact]
    public void Validate_NullBody_ReturnsFalse()
    {
        var validator = MakeValidator();
        var (ts, sig) = ComputeSignature("body", TestSecret, FixedNow);
        validator.Validate(ts, null!, sig, FixedNow).Should().BeFalse();
    }

    [Fact]
    public void Validate_NullSignature_ReturnsFalse()
    {
        var validator = MakeValidator();
        var (ts, _) = ComputeSignature("body", TestSecret, FixedNow);
        validator.Validate(ts, "body", null!, FixedNow).Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptySignature_ReturnsFalse()
    {
        var validator = MakeValidator();
        var (ts, _) = ComputeSignature("body", TestSecret, FixedNow);
        validator.Validate(ts, "body", string.Empty, FixedNow).Should().BeFalse();
    }

    [Fact]
    public void Validate_NowParameterInjectionMakesTestDeterministic()
    {
        var validator = MakeValidator();
        var body = "payload=test";
        var (ts, sig) = ComputeSignature(body, TestSecret, FixedNow);

        // Same now → true
        validator.Validate(ts, body, sig, FixedNow).Should().BeTrue();
        // Different now that would make it expired → false
        validator.Validate(ts, body, sig, FixedNow.AddSeconds(600)).Should().BeFalse();
    }
}
