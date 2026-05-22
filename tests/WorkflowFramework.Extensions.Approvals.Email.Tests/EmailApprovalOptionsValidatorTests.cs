using FluentAssertions;
using Microsoft.Extensions.Options;
using WorkflowFramework.Extensions.Approvals.Email;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Email.Tests;

public sealed class EmailApprovalOptionsValidatorTests
{
    private static readonly EmailApprovalOptionsValidator Validator = new();

    private static EmailApprovalOptions ValidOptions() => new()
    {
        SmtpHost = "smtp.example.com",
        From = "approvals@example.com",
        ApproveUrlTemplate = "https://example.com/approve?t={token}",
        RejectUrlTemplate = "https://example.com/reject?t={token}",
        TokenSigningKey = Convert.ToBase64String(new byte[32])
    };

    // ------------------------------------------------------------------
    // Valid options succeed
    // ------------------------------------------------------------------

    [Fact]
    public void ValidOptions_ReturnsSuccess()
    {
        var result = Validator.Validate(null, ValidOptions());
        result.Succeeded.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // Required fields missing
    // ------------------------------------------------------------------

    [Fact]
    public void MissingSmtpHost_Fails_WithMessage()
    {
        var opts = ValidOptions();
        opts.SmtpHost = string.Empty;

        var result = Validator.Validate(null, opts);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(EmailApprovalOptions.SmtpHost));
    }

    [Fact]
    public void MissingFrom_Fails_WithMessage()
    {
        var opts = ValidOptions();
        opts.From = string.Empty;

        var result = Validator.Validate(null, opts);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(EmailApprovalOptions.From));
    }

    [Fact]
    public void MissingApproveUrlTemplate_Fails_WithMessage()
    {
        var opts = ValidOptions();
        opts.ApproveUrlTemplate = string.Empty;

        var result = Validator.Validate(null, opts);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(EmailApprovalOptions.ApproveUrlTemplate));
    }

    [Fact]
    public void MissingRejectUrlTemplate_Fails_WithMessage()
    {
        var opts = ValidOptions();
        opts.RejectUrlTemplate = string.Empty;

        var result = Validator.Validate(null, opts);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(EmailApprovalOptions.RejectUrlTemplate));
    }

    // ------------------------------------------------------------------
    // Token placeholder validation
    // ------------------------------------------------------------------

    [Fact]
    public void ApproveUrlTemplate_MissingTokenPlaceholder_Fails()
    {
        var opts = ValidOptions();
        opts.ApproveUrlTemplate = "https://example.com/approve";

        var result = Validator.Validate(null, opts);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("{token}");
    }

    [Fact]
    public void RejectUrlTemplate_MissingTokenPlaceholder_Fails()
    {
        var opts = ValidOptions();
        opts.RejectUrlTemplate = "https://example.com/reject";

        var result = Validator.Validate(null, opts);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("{token}");
    }

    // ------------------------------------------------------------------
    // TokenSigningKey validation
    // ------------------------------------------------------------------

    [Fact]
    public void TokenSigningKey_NotBase64_Fails()
    {
        var opts = ValidOptions();
        opts.TokenSigningKey = "not-valid-base64!!!";

        var result = Validator.Validate(null, opts);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(EmailApprovalOptions.TokenSigningKey));
    }

    [Fact]
    public void TokenSigningKey_ValidBase64_ButLessThan32Bytes_Fails()
    {
        var opts = ValidOptions();
        opts.TokenSigningKey = Convert.ToBase64String(new byte[16]); // Only 16 bytes

        var result = Validator.Validate(null, opts);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("32");
    }

    [Fact]
    public void TokenSigningKey_Exactly32Bytes_Succeeds()
    {
        var opts = ValidOptions();
        opts.TokenSigningKey = Convert.ToBase64String(new byte[32]);

        var result = Validator.Validate(null, opts);

        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void TokenSigningKey_MoreThan32Bytes_Succeeds()
    {
        var opts = ValidOptions();
        opts.TokenSigningKey = Convert.ToBase64String(new byte[64]);

        var result = Validator.Validate(null, opts);

        result.Succeeded.Should().BeTrue();
    }
}
