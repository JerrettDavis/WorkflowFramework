using FluentAssertions;
using Microsoft.Extensions.Options;
using WorkflowFramework.Extensions.Approvals.Slack;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Slack.Tests;

public sealed class SlackApprovalOptionsValidatorTests
{
    private static SlackApprovalOptionsValidator MakeValidator() => new();

    private static SlackApprovalOptions ValidOptions() =>
        new()
        {
            BotToken = "xoxb-valid-token",
            ChannelId = "C12345",
            SigningSecret = "mysigningsecret",
            ApiBaseUrl = "https://slack.com/api/",
            RequestSignatureMaxAgeSeconds = 300
        };

    [Fact]
    public void Validate_ValidOptions_ReturnsSuccess()
    {
        var result = MakeValidator().Validate(null, ValidOptions());
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingBotToken_Fails()
    {
        var opts = ValidOptions();
        opts.BotToken = string.Empty;

        var result = MakeValidator().Validate(null, opts);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(SlackApprovalOptions.BotToken));
    }

    [Fact]
    public void Validate_MissingChannelId_Fails()
    {
        var opts = ValidOptions();
        opts.ChannelId = string.Empty;

        var result = MakeValidator().Validate(null, opts);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(SlackApprovalOptions.ChannelId));
    }

    [Fact]
    public void Validate_MissingSigningSecret_Fails()
    {
        var opts = ValidOptions();
        opts.SigningSecret = string.Empty;

        var result = MakeValidator().Validate(null, opts);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(SlackApprovalOptions.SigningSecret));
    }

    [Fact]
    public void Validate_InvalidApiBaseUrl_Fails()
    {
        var opts = ValidOptions();
        opts.ApiBaseUrl = "not a valid uri!!!";

        var result = MakeValidator().Validate(null, opts);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(SlackApprovalOptions.ApiBaseUrl));
    }

    [Fact]
    public void Validate_MaxAgeZero_Fails()
    {
        var opts = ValidOptions();
        opts.RequestSignatureMaxAgeSeconds = 0;

        var result = MakeValidator().Validate(null, opts);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(SlackApprovalOptions.RequestSignatureMaxAgeSeconds));
    }

    [Fact]
    public void Validate_MaxAgeNegative_Fails()
    {
        var opts = ValidOptions();
        opts.RequestSignatureMaxAgeSeconds = -1;

        var result = MakeValidator().Validate(null, opts);
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(SlackApprovalOptions.RequestSignatureMaxAgeSeconds));
    }

    [Fact]
    public void Validate_AllFieldsMissing_ReportsMultipleErrors()
    {
        var opts = new SlackApprovalOptions
        {
            BotToken = string.Empty,
            ChannelId = string.Empty,
            SigningSecret = string.Empty,
            ApiBaseUrl = string.Empty,
            RequestSignatureMaxAgeSeconds = 0
        };

        var result = MakeValidator().Validate(null, opts);
        result.Failed.Should().BeTrue();
        // Multiple error messages
        result.Failures.Should().HaveCountGreaterThan(1);
    }
}
