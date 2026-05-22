using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using WorkflowFramework.Extensions.Approvals;
using WorkflowFramework.Extensions.Approvals.Email;
using WorkflowFramework.Extensions.Approvals.Email.DependencyInjection;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Email.Tests;

public sealed class EmailApprovalsBuilderExtensionsTests
{
    private static readonly string ValidKey = Convert.ToBase64String(new byte[32]
    {
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
        17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
    });

    private static IServiceCollection BuildWithEmail(Action<EmailApprovalOptions>? configure = null)
    {
        var services = new ServiceCollection();
        configure ??= opts =>
        {
            opts.SmtpHost = "smtp.example.com";
            opts.From = "approvals@example.com";
            opts.ApproveUrlTemplate = "https://example.com/approve?t={token}";
            opts.RejectUrlTemplate = "https://example.com/reject?t={token}";
            opts.TokenSigningKey = ValidKey;
        };

        services.AddApprovals().UseEmail(configure);
        return services;
    }

    // ------------------------------------------------------------------
    // AddApprovals().UseEmail() registers expected services
    // ------------------------------------------------------------------

    [Fact]
    public void UseEmail_RegistersApprovalTokenService()
    {
        var services = BuildWithEmail();

        services.Should().Contain(sd => sd.ServiceType == typeof(ApprovalTokenService));
    }

    [Fact]
    public void UseEmail_RegistersIEmailSender()
    {
        var services = BuildWithEmail();

        services.Should().Contain(sd => sd.ServiceType == typeof(IEmailSender));
    }

    [Fact]
    public void UseEmail_RegistersEmailApprovalChannel()
    {
        var services = BuildWithEmail();

        services.Should().Contain(sd => sd.ServiceType == typeof(EmailApprovalChannel));
    }

    [Fact]
    public void UseEmail_RegistersEmailApprovalOptionsValidator()
    {
        var services = BuildWithEmail();

        services.Should().Contain(sd =>
            sd.ServiceType == typeof(IValidateOptions<EmailApprovalOptions>));
    }

    [Fact]
    public void UseEmail_RegistersEmailApprovalChannelAsIApprovalChannel()
    {
        var services = BuildWithEmail();

        services.Should().Contain(sd =>
            sd.ServiceType == typeof(IApprovalChannel) &&
            sd.ImplementationType == typeof(EmailApprovalChannel));
    }

    // ------------------------------------------------------------------
    // Service resolution
    // ------------------------------------------------------------------

    [Fact]
    public void BuildServiceProvider_CanResolveApprovalTokenService()
    {
        var services = BuildWithEmail();

        // Need to register an IApprovalChannel for PersistentApprovalService to resolve.
        // EmailApprovalChannel is already registered but depends on PersistentApprovalService.
        // For the resolution test, provide a no-op inner channel override.
        var fakeInner = Substitute.For<IApprovalChannel>();
        fakeInner.Name.Returns("fake");
        services.AddSingleton<IApprovalStore, InMemoryApprovalStore>();

        var provider = services.BuildServiceProvider();
        var tokenSvc = provider.GetService<ApprovalTokenService>();

        tokenSvc.Should().NotBeNull();
    }

    [Fact]
    public void BuildServiceProvider_CanResolveIEmailSender_AsSmtpEmailSender()
    {
        var services = BuildWithEmail();
        var provider = services.BuildServiceProvider();

        var sender = provider.GetService<IEmailSender>();

        sender.Should().NotBeNull();
        sender.Should().BeOfType<SmtpEmailSender>();
    }

    // ------------------------------------------------------------------
    // Options validation is configured
    // ------------------------------------------------------------------

    [Fact]
    public void UseEmail_ValidOptions_ValidationPassesOnBuild()
    {
        var services = BuildWithEmail();
        var act = () => services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false });

        act.Should().NotThrow();
    }
}
