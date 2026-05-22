using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// Extension methods for registering the approvals orchestrator with
/// <see cref="IServiceCollection"/>.
/// </summary>
public static class ApprovalsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core approvals orchestrator services and returns a fluent
    /// <see cref="IApprovalsBuilder"/> for further configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Default registrations (all use <c>TryAdd</c> / <c>TryAddSingleton</c> so they can be
    /// overridden by the caller):
    /// </para>
    /// <list type="bullet">
    ///   <item><see cref="InMemoryApprovalStore"/> as <see cref="IApprovalStore"/>.</item>
    ///   <item><see cref="PersistentApprovalService"/> as a singleton (not as
    ///   <see cref="IApprovalChannel"/> — it is wired into the pipeline by the builder).</item>
    ///   <item><see cref="NamedChannelRouter"/> as <see cref="IApprovalRouter"/>.</item>
    ///   <item><see cref="ApprovalRehydrationHostedService"/> as a hosted service.</item>
    /// </list>
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>
    /// An <see cref="IApprovalsBuilder"/> for chaining additional configuration calls.
    /// </returns>
    public static IApprovalsBuilder AddApprovals(this IServiceCollection services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));

        services.TryAddSingleton<IApprovalStore, InMemoryApprovalStore>();
        services.TryAddSingleton<PersistentApprovalService>();
        services.TryAddSingleton<IApprovalRouter, NamedChannelRouter>();
        services.AddHostedService<ApprovalRehydrationHostedService>();

        return new ApprovalsBuilder(services);
    }
}
