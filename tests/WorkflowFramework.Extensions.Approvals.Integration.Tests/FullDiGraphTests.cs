using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.Approvals;
using WorkflowFramework.Extensions.Approvals.Cli;
using WorkflowFramework.Extensions.Approvals.Cli.DependencyInjection;
using WorkflowFramework.Extensions.Approvals.Email;
using WorkflowFramework.Extensions.Approvals.Email.DependencyInjection;
using WorkflowFramework.Extensions.Approvals.Slack;
using WorkflowFramework.Extensions.Approvals.Slack.DependencyInjection;
using WorkflowFramework.Extensions.Approvals.Teams;
using WorkflowFramework.Extensions.Approvals.Teams.DependencyInjection;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Integration.Tests;

/// <summary>
/// Smoke tests that verify the full DI graph resolves without circular dependency
/// exceptions when all approval channels are registered together.
/// </summary>
public sealed class FullDiGraphTests
{
    private static IServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddApprovals()
            .UseCli()
            .UseEmail(opts =>
            {
                opts.SmtpHost = "smtp.test.local";
                opts.SmtpPort = 25;
                opts.From = "approvals@test.local";
                opts.ApproveUrlTemplate = "https://test.local/approvals/email/respond?t={token}&action=approve";
                opts.RejectUrlTemplate = "https://test.local/approvals/email/respond?t={token}&action=reject";
                opts.TokenSigningKey = Convert.ToBase64String(new byte[32]
                {
                    1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
                    17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
                });
            })
            .UseSlack(opts =>
            {
                opts.BotToken = "xoxb-test-integration";
                opts.ChannelId = "C_INTEGRATION";
                opts.SigningSecret = "integration-signing-secret";
            })
            .UseTeams(opts =>
            {
                opts.Mode = TeamsApprovalMode.IncomingWebhook;
                opts.WebhookUrl = "https://outlook.office.com/webhook/integration-test";
                opts.CallbackSharedSecret = "integration-callback-secret";
            });

        return services;
    }

    [Fact]
    public void Full_DI_graph_resolves_without_circular_dependency()
    {
        // Arrange
        var services = BuildServices();

        // Act — this will throw if there is a circular dependency or misconfiguration
        using var provider = services.BuildServiceProvider(validateScopes: true);

        // Force resolution of the full IApprovalChannel enumerable (which includes all 4 channels)
        var channels = provider.GetServices<IApprovalChannel>().ToList();

        // Assert
        channels.Should().NotBeEmpty("at least one channel should be registered");
    }

    [Fact]
    public void CLI_channel_resolves_from_DI()
    {
        var services = BuildServices();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var channel = provider.GetRequiredService<CliApprovalChannel>();

        channel.Should().NotBeNull();
        channel.Name.Should().Be("cli");
    }

    [Fact]
    public void Email_channel_resolves_from_DI()
    {
        var services = BuildServices();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var channel = provider.GetRequiredService<EmailApprovalChannel>();

        channel.Should().NotBeNull();
        channel.Name.Should().Be("email");
    }

    [Fact]
    public void Slack_channel_resolves_from_DI()
    {
        var services = BuildServices();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var channel = provider.GetRequiredService<SlackApprovalChannel>();

        channel.Should().NotBeNull();
        channel.Name.Should().Be("slack");
    }

    [Fact]
    public void Teams_channel_resolves_from_DI()
    {
        var services = BuildServices();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var channel = provider.GetRequiredService<TeamsApprovalChannel>();

        channel.Should().NotBeNull();
        channel.Name.Should().Be("teams");
    }

    [Fact]
    public void PersistentApprovalService_resolves_from_DI()
    {
        var services = BuildServices();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var persistent = provider.GetRequiredService<PersistentApprovalService>();

        persistent.Should().NotBeNull();
    }

    [Fact]
    public void All_four_channels_are_registered_as_IApprovalChannel()
    {
        var services = BuildServices();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var channels = provider.GetServices<IApprovalChannel>().ToList();
        var channelNames = channels.Select(c => c.Name).ToList();

        channelNames.Should().Contain("cli", "CLI channel should be registered");
        channelNames.Should().Contain("email", "Email channel should be registered");
        channelNames.Should().Contain("slack", "Slack channel should be registered");
        channelNames.Should().Contain("teams", "Teams channel should be registered");
    }
}
