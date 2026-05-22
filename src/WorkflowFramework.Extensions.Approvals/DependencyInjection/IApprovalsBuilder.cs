using Microsoft.Extensions.DependencyInjection;

namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// Provides a fluent API for configuring the approvals orchestrator during application startup.
/// </summary>
/// <remarks>
/// Obtain an instance via <see cref="ApprovalsServiceCollectionExtensions.AddApprovals"/>.
/// </remarks>
public interface IApprovalsBuilder
{
    /// <summary>Gets the underlying <see cref="IServiceCollection"/> being configured.</summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Configures the minimum number of approvals required to satisfy quorum.
    /// </summary>
    /// <param name="requiredApprovers">Must be greater than or equal to 1.</param>
    /// <returns>The same builder for chaining.</returns>
    IApprovalsBuilder WithQuorum(int requiredApprovers);

    /// <summary>
    /// Sets the default approval timeout and the action to take when it elapses.
    /// </summary>
    /// <param name="timeout">The time to wait before the <paramref name="onTimeout"/> action fires.</param>
    /// <param name="onTimeout">Defaults to <see cref="OnTimeoutAction.AutoReject"/>.</param>
    /// <returns>The same builder for chaining.</returns>
    IApprovalsBuilder WithTimeout(TimeSpan timeout, OnTimeoutAction onTimeout = OnTimeoutAction.AutoReject);

    /// <summary>
    /// Configures a multi-step escalation chain.
    /// </summary>
    /// <param name="configure">A delegate that configures the escalation steps.</param>
    /// <returns>The same builder for chaining.</returns>
    IApprovalsBuilder WithEscalation(Action<IEscalationBuilder> configure);

    /// <summary>
    /// Registers a custom <see cref="IApprovalStore"/> implementation in place of the default
    /// <see cref="InMemoryApprovalStore"/>.
    /// </summary>
    /// <typeparam name="TStore">The store type to register.</typeparam>
    /// <returns>The same builder for chaining.</returns>
    IApprovalsBuilder WithPersistence<TStore>() where TStore : class, IApprovalStore;

    /// <summary>
    /// Registers an <see cref="IApprovalChannel"/> implementation by type.
    /// </summary>
    /// <typeparam name="TChannel">The channel type to register.</typeparam>
    /// <returns>The same builder for chaining.</returns>
    IApprovalsBuilder UseChannel<TChannel>() where TChannel : class, IApprovalChannel;

    /// <summary>
    /// Registers a pre-built <see cref="IApprovalChannel"/> instance.
    /// </summary>
    /// <param name="instance">The channel instance to register.</param>
    /// <returns>The same builder for chaining.</returns>
    IApprovalsBuilder UseChannel(IApprovalChannel instance);
}

/// <summary>
/// Provides a fluent API for building an escalation chain inside
/// <see cref="IApprovalsBuilder.WithEscalation"/>.
/// </summary>
public interface IEscalationBuilder
{
    /// <summary>
    /// Specifies the channel to escalate from.
    /// </summary>
    /// <typeparam name="TChannel">The primary (source) channel type.</typeparam>
    /// <returns>An <see cref="IEscalationStep"/> to configure timing and destination.</returns>
    IEscalationStep From<TChannel>() where TChannel : class, IApprovalChannel;
}

/// <summary>
/// Represents a single step in an escalation chain, connecting a source channel to a
/// destination channel after a configurable delay.
/// </summary>
public interface IEscalationStep
{
    /// <summary>
    /// Sets the time to wait on the current channel before escalating.
    /// </summary>
    /// <param name="timeout">The escalation timeout for this step.</param>
    /// <returns>The same step for chaining.</returns>
    IEscalationStep After(TimeSpan timeout);

    /// <summary>
    /// Specifies the channel to escalate to after the timeout elapses.
    /// </summary>
    /// <typeparam name="TChannel">The destination channel type.</typeparam>
    /// <returns>The parent <see cref="IEscalationBuilder"/> for further configuration.</returns>
    IEscalationBuilder To<TChannel>() where TChannel : class, IApprovalChannel;
}
