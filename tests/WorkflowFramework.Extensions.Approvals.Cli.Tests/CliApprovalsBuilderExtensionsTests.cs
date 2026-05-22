using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.Approvals.Cli.Commands;
using WorkflowFramework.Extensions.Approvals.Cli.DependencyInjection;
using Xunit;

namespace WorkflowFramework.Extensions.Approvals.Cli.Tests;

/// <summary>
/// Unit tests for <see cref="CliApprovalsBuilderExtensions"/>.
/// </summary>
public sealed class CliApprovalsBuilderExtensionsTests
{
    /// <summary>
    /// Verifies that calling <c>AddApprovals().UseCli()</c> registers
    /// <see cref="IConsole"/> and <see cref="CliApprovalChannel"/> so they can be resolved
    /// from the service provider.
    /// </summary>
    [Fact]
    public void UseCli_RegistersIConsoleAndCliApprovalChannel()
    {
        var services = new ServiceCollection();
        services.AddApprovals().UseCli();

        var provider = services.BuildServiceProvider();

        var console = provider.GetService<IConsole>();
        console.Should().NotBeNull()
            .And.BeOfType<SystemConsole>();

        var channel = provider.GetService<CliApprovalChannel>();
        channel.Should().NotBeNull();
    }

    /// <summary>
    /// Verifies that <see cref="CliApprovalChannel"/> is registered as an
    /// <see cref="IApprovalChannel"/> in the channel enumeration.
    /// </summary>
    [Fact]
    public void UseCli_RegistersCliApprovalChannelAsIApprovalChannel()
    {
        var services = new ServiceCollection();
        services.AddApprovals().UseCli();

        var provider = services.BuildServiceProvider();

        var channels = provider.GetServices<IApprovalChannel>().ToList();
        channels.Should().Contain(c => c is CliApprovalChannel);
    }

    /// <summary>
    /// Verifies that <see cref="CliApprovalsBuilderExtensions.UseCli"/> throws
    /// <see cref="ArgumentNullException"/> when the builder is null.
    /// </summary>
    [Fact]
    public void UseCli_NullBuilder_ThrowsArgumentNullException()
    {
        IApprovalsBuilder? builder = null;
        var act = () => builder!.UseCli();
        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }
}
