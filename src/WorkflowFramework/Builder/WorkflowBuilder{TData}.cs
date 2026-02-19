using WorkflowFramework.Internal;

namespace WorkflowFramework.Builder;

/// <summary>
/// Default implementation of <see cref="IWorkflowBuilder{TData}"/>.
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
public sealed class WorkflowBuilder<TData> : IWorkflowBuilder<TData> where TData : class
{
    private readonly List<IStep> _steps = new();
    private readonly List<IWorkflowMiddleware> _middleware = new();
    private readonly List<IWorkflowEvents> _events = new();
    private IServiceProvider? _serviceProvider;
    private string _name = "Workflow";
    private bool _enableCompensation;

    /// <inheritdoc />
    public IWorkflowBuilder<TData> Step<TStep>() where TStep : IStep<TData>, new()
    {
        _steps.Add(new TypedStepAdapter<TData>(new TStep()));
        return this;
    }

    /// <inheritdoc />
    public IWorkflowBuilder<TData> Step(IStep<TData> step)
    {
        _steps.Add(new TypedStepAdapter<TData>(step ?? throw new ArgumentNullException(nameof(step))));
        return this;
    }

    /// <inheritdoc />
    public IWorkflowBuilder<TData> Step(string name, Func<IWorkflowContext<TData>, Task> action)
    {
        _steps.Add(new TypedStepAdapter<TData>(new DelegateStep<TData>(name, action)));
        return this;
    }

    /// <inheritdoc />
    public IConditionalBuilder<TData> If(Func<IWorkflowContext<TData>, bool> condition) =>
        new ConditionalBuilderImpl(this, condition);

    /// <inheritdoc />
    public IWorkflowBuilder<TData> Parallel(Action<IParallelBuilder<TData>> configure)
    {
        var builder = new ParallelBuilderImpl();
        configure(builder);
        _steps.Add(new ParallelStep(builder.Steps));
        return this;
    }

    /// <inheritdoc />
    public IWorkflowBuilder<TData> Use<TMiddleware>() where TMiddleware : IWorkflowMiddleware, new()
    {
        _middleware.Add(new TMiddleware());
        return this;
    }

    /// <inheritdoc />
    public IWorkflowBuilder<TData> Use(IWorkflowMiddleware middleware)
    {
        _middleware.Add(middleware ?? throw new ArgumentNullException(nameof(middleware)));
        return this;
    }

    /// <inheritdoc />
    public IWorkflowBuilder<TData> WithEvents(IWorkflowEvents events)
    {
        _events.Add(events ?? throw new ArgumentNullException(nameof(events)));
        return this;
    }

    /// <inheritdoc />
    public IWorkflowBuilder<TData> WithServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        return this;
    }

    /// <inheritdoc />
    public IWorkflowBuilder<TData> WithName(string name)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        return this;
    }

    /// <inheritdoc />
    public IWorkflowBuilder<TData> WithCompensation()
    {
        _enableCompensation = true;
        return this;
    }

    /// <inheritdoc />
    public IWorkflow<TData> Build()
    {
        var engine = new WorkflowEngine(_name, _steps.ToArray(), _middleware.ToArray(), _events.ToArray(), _enableCompensation);
        return new TypedWorkflowAdapter<TData>(engine);
    }

    internal void AddStep(IStep step) => _steps.Add(step);

    private sealed class ConditionalBuilderImpl(WorkflowBuilder<TData> parent, Func<IWorkflowContext<TData>, bool> condition)
        : IConditionalBuilder<TData>
    {
        public IElseBuilder<TData> Then<TStep>() where TStep : IStep<TData>, new() =>
            Then(new TStep());

        public IElseBuilder<TData> Then(IStep<TData> step) =>
            new ElseBuilderImpl(parent, condition, step);
    }

    private sealed class ElseBuilderImpl(
        WorkflowBuilder<TData> parent,
        Func<IWorkflowContext<TData>, bool> condition,
        IStep<TData> thenStep
    )
        : IElseBuilder<TData>
    {
        public IWorkflowBuilder<TData> Else<TStep>() where TStep : IStep<TData>, new() =>
            Else(new TStep());

        public IWorkflowBuilder<TData> Else(IStep<TData> step)
        {
            parent.AddStep(new ConditionalStep<TData>(condition, thenStep, step));
            return parent;
        }

        public IWorkflowBuilder<TData> EndIf()
        {
            parent.AddStep(new ConditionalStep<TData>(condition, thenStep, null));
            return parent;
        }
    }

    private sealed class ParallelBuilderImpl : IParallelBuilder<TData>
    {
        private readonly List<IStep> _steps = new();

        public IReadOnlyList<IStep> Steps => _steps;

        public IParallelBuilder<TData> Step<TStep>() where TStep : IStep<TData>, new()
        {
            _steps.Add(new TypedStepAdapter<TData>(new TStep()));
            return this;
        }

        public IParallelBuilder<TData> Step(IStep<TData> step)
        {
            _steps.Add(new TypedStepAdapter<TData>(step ?? throw new ArgumentNullException(nameof(step))));
            return this;
        }
    }
}
