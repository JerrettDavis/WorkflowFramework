namespace WorkflowFramework;

/// <summary>
/// Default implementation of <see cref="IWorkflowContext"/>.
/// </summary>
public class WorkflowContext : IWorkflowContext
{
    /// <summary>
    /// Initializes a new instance of <see cref="WorkflowContext"/>.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public WorkflowContext(CancellationToken cancellationToken = default)
    {
        WorkflowId = Guid.NewGuid().ToString("N");
        CorrelationId = Guid.NewGuid().ToString("N");
        CancellationToken = cancellationToken;
    }

    /// <inheritdoc />
    public string WorkflowId { get; }

    /// <inheritdoc />
    public string CorrelationId { get; }

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; }

    /// <inheritdoc />
    public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();

    /// <inheritdoc />
    public string? CurrentStepName { get; set; }

    /// <inheritdoc />
    public int CurrentStepIndex { get; set; }

    /// <inheritdoc />
    public bool IsAborted { get; set; }

    /// <inheritdoc />
    public IList<WorkflowError> Errors { get; } = new List<WorkflowError>();
}

/// <summary>
/// Default implementation of <see cref="IWorkflowContext{TData}"/>.
/// </summary>
/// <typeparam name="TData">The type of the workflow data.</typeparam>
public class WorkflowContext<TData> : WorkflowContext, IWorkflowContext<TData>
    where TData : class
{
    /// <summary>
    /// Initializes a new instance of <see cref="WorkflowContext{TData}"/>.
    /// </summary>
    /// <param name="data">The initial data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public WorkflowContext(TData data, CancellationToken cancellationToken = default)
        : base(cancellationToken)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <inheritdoc />
    public TData Data { get; set; }
}
