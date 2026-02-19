using WorkflowFramework.Internal;

namespace WorkflowFramework.Builder;

/// <summary>
/// Default implementation of <see cref="IWorkflowBuilder"/>.
/// </summary>
public sealed class WorkflowBuilder : IWorkflowBuilder
{
    private readonly List<IStep> _steps = new();
    private readonly List<IWorkflowMiddleware> _middleware = new();
    private readonly List<IWorkflowEvents> _events = new();
    private IServiceProvider? _serviceProvider;
    private string _name = "Workflow";
    private bool _enableCompensation;

    /// <inheritdoc />
    public IWorkflowBuilder Step<TStep>() where TStep : IStep, new()
    {
        _steps.Add(new TStep());
        return this;
    }

    /// <inheritdoc />
    public IWorkflowBuilder Step(IStep step)
    {
        _steps.Add(step ?? throw new ArgumentNullException(nameof(step)));
        return this;
    }

    /// <inheritdoc />
    public IWorkflowBuilder Step(string name, Func<IWorkflowContext, Task> action)
    {
        _steps.Add(new DelegateStep(name, action));
        return this;
    }

    /// <inheritdoc />
    public IConditionalBuilder If(Func<IWorkflowContext, bool> condition) =>
        new ConditionalBuilderImpl(this, condition);

    /// <inheritdoc />
    public IWorkflowBuilder Parallel(Action<IParallelBuilder> configure)
    {
        var builder = new ParallelBuilderImpl();
        configure(builder);
        _steps.Add(new ParallelStep(builder.Steps));
        return this;
    }

    /// <inheritdoc />
    public IWorkflowBuilder Use<TMiddleware>() where TMiddleware : IWorkflowMiddleware, new()
    {
        _middleware.Add(new TMiddleware());
        return this;
    }

    /// <inheritdoc />
    public IWorkflowBuilder Use(IWorkflowMiddleware middleware)
    {
        _middleware.Add(middleware ?? throw new ArgumentNullException(nameof(middleware)));
        return this;
    }

    /// <inheritdoc />
    public IWorkflowBuilder WithEvents(IWorkflowEvents events)
    {
        _events.Add(events ?? throw new ArgumentNullException(nameof(events)));
        return this;
    }

    /// <inheritdoc />
    public IWorkflowBuilder WithServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        return this;
    }

    /// <inheritdoc />
    public IWorkflowBuilder WithName(string name)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        return this;
    }

    /// <inheritdoc />
    public IWorkflowBuilder WithCompensation()
    {
        _enableCompensation = true;
        return this;
    }

    /// <inheritdoc />
    public IWorkflow Build() =>
        new WorkflowEngine(_name, _steps.ToArray(), _middleware.ToArray(), _events.ToArray(), _enableCompensation);

    internal void AddStep(IStep step) => _steps.Add(step);

    private sealed class ConditionalBuilderImpl(WorkflowBuilder parent, Func<IWorkflowContext, bool> condition) : IConditionalBuilder
    {
        public IElseBuilder Then<TStep>() where TStep : IStep, new() => Then(new TStep());

        public IElseBuilder Then(IStep step) => new ElseBuilderImpl(parent, condition, step);
    }

    private sealed class ElseBuilderImpl(WorkflowBuilder parent, Func<IWorkflowContext, bool> condition, IStep thenStep)
        : IElseBuilder
    {
        public IWorkflowBuilder Else<TStep>() where TStep : IStep, new() => Else(new TStep());

        public IWorkflowBuilder Else(IStep step)
        {
            parent.AddStep(new ConditionalStep(condition, thenStep, step));
            return parent;
        }

        public IWorkflowBuilder EndIf()
        {
            parent.AddStep(new ConditionalStep(condition, thenStep, null));
            return parent;
        }
    }

    private sealed class ParallelBuilderImpl : IParallelBuilder
    {
        private readonly List<IStep> _steps = new();

        public IReadOnlyList<IStep> Steps => _steps;

        public IParallelBuilder Step<TStep>() where TStep : IStep, new()
        {
            _steps.Add(new TStep());
            return this;
        }

        public IParallelBuilder Step(IStep step)
        {
            _steps.Add(step ?? throw new ArgumentNullException(nameof(step)));
            return this;
        }
    }
}
