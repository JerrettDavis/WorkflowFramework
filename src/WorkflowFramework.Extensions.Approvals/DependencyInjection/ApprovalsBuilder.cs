using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WorkflowFramework.Extensions.Approvals;

/// <summary>
/// Concrete implementation of <see cref="IApprovalsBuilder"/> that accumulates configuration
/// and registers services against the underlying <see cref="IServiceCollection"/>.
/// </summary>
internal sealed class ApprovalsBuilder : IApprovalsBuilder
{
    private readonly List<EscalationRule> _escalationRules = new();

    /// <inheritdoc />
    public IServiceCollection Services { get; }

    internal ApprovalsBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <inheritdoc />
    public IApprovalsBuilder WithQuorum(int requiredApprovers)
    {
        Services.Configure<ApprovalsOptions>(o => o.RequiredApprovers = requiredApprovers);
        return this;
    }

    /// <inheritdoc />
    public IApprovalsBuilder WithTimeout(TimeSpan timeout, OnTimeoutAction onTimeout = OnTimeoutAction.AutoReject)
    {
        Services.Configure<ApprovalsOptions>(o =>
        {
            o.Timeout = timeout;
            o.OnTimeoutAction = onTimeout;
        });
        return this;
    }

    /// <inheritdoc />
    public IApprovalsBuilder WithEscalation(Action<IEscalationBuilder> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var escalationBuilder = new EscalationBuilder(_escalationRules);
        configure(escalationBuilder);
        return this;
    }

    /// <inheritdoc />
    public IApprovalsBuilder WithPersistence<TStore>() where TStore : class, IApprovalStore
    {
        // Remove any previous TryAddSingleton registration for IApprovalStore.
        var existing = Services.FirstOrDefault(sd => sd.ServiceType == typeof(IApprovalStore));
        if (existing is not null)
            Services.Remove(existing);

        Services.AddSingleton<IApprovalStore, TStore>();
        return this;
    }

    /// <inheritdoc />
    public IApprovalsBuilder UseChannel<TChannel>() where TChannel : class, IApprovalChannel
    {
        Services.TryAddEnumerable(ServiceDescriptor.Singleton<IApprovalChannel, TChannel>());
        return this;
    }

    /// <inheritdoc />
    public IApprovalsBuilder UseChannel(IApprovalChannel instance)
    {
        if (instance is null) throw new ArgumentNullException(nameof(instance));
        Services.TryAddEnumerable(ServiceDescriptor.Singleton(instance));
        return this;
    }

    // -------------------------------------------------------------------------
    // Escalation builder helpers
    // -------------------------------------------------------------------------

    private sealed class EscalationBuilder : IEscalationBuilder
    {
        private readonly List<EscalationRule> _rules;

        internal EscalationBuilder(List<EscalationRule> rules) => _rules = rules;

        public IEscalationStep From<TChannel>() where TChannel : class, IApprovalChannel
        {
            var rule = new EscalationRule(typeof(TChannel));
            _rules.Add(rule);
            return new EscalationStep(rule, this);
        }
    }

    private sealed class EscalationStep : IEscalationStep
    {
        private readonly EscalationRule _rule;
        private readonly IEscalationBuilder _parent;

        internal EscalationStep(EscalationRule rule, IEscalationBuilder parent)
        {
            _rule = rule;
            _parent = parent;
        }

        public IEscalationStep After(TimeSpan timeout)
        {
            _rule.After = timeout;
            return this;
        }

        public IEscalationBuilder To<TChannel>() where TChannel : class, IApprovalChannel
        {
            _rule.ToType = typeof(TChannel);
            return _parent;
        }
    }

    private sealed class EscalationRule
    {
        internal Type FromType { get; }
        internal Type? ToType { get; set; }
        internal TimeSpan After { get; set; } = TimeSpan.FromHours(1);

        internal EscalationRule(Type fromType) => FromType = fromType;
    }
}
