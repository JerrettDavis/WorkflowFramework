using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.Approvals.Cli.Commands;

namespace WorkflowFramework.Extensions.Approvals.Cli.DependencyInjection;

/// <summary>
/// Extension methods for registering the CLI approval channel with an
/// <see cref="IApprovalsBuilder"/>.
/// </summary>
public static class CliApprovalsBuilderExtensions
{
    /// <summary>
    /// Registers the CLI approval channel and its supporting services so that the
    /// approvals orchestrator will dispatch requests to the console and wait for an
    /// operator to respond with <c>wf approvals approve/reject</c>.
    /// </summary>
    /// <param name="builder">
    /// The approvals builder returned by
    /// <c>services.AddApprovals()</c>. Must not be <see langword="null"/>.
    /// </param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    public static IApprovalsBuilder UseCli(this IApprovalsBuilder builder)
    {
        if (builder is null) throw new ArgumentNullException(nameof(builder));

        builder.Services.AddSingleton<IConsole, SystemConsole>();

        // Register Lazy<PersistentApprovalService> to break the circular reference:
        // CliApprovalChannel -> Lazy<PersistentApprovalService>
        //   (deferred) -> PersistentApprovalService -> IApprovalChannel -> CliApprovalChannel
        builder.Services.AddSingleton(sp => new Lazy<PersistentApprovalService>(
            sp.GetRequiredService<PersistentApprovalService>));

        builder.Services.AddSingleton<CliApprovalChannel>();
        builder.UseChannel<CliApprovalChannel>();

        return builder;
    }
}
