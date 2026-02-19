namespace WorkflowFramework.Internal;

/// <summary>
/// Adapts an <see cref="IWorkflow"/> to an <see cref="IWorkflow{TData}"/>.
/// </summary>
internal sealed class TypedWorkflowAdapter<TData>(IWorkflow inner) : IWorkflow<TData>
    where TData : class
{
    private readonly IWorkflow _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public string Name => _inner.Name;

    public async Task<WorkflowResult<TData>> ExecuteAsync(IWorkflowContext<TData> context)
    {
        var result = await _inner.ExecuteAsync(context).ConfigureAwait(false);
        return new WorkflowResult<TData>(result.Status, (IWorkflowContext<TData>)result.Context);
    }
}
