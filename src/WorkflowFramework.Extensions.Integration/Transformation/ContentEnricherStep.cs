namespace WorkflowFramework.Extensions.Integration.Transformation;

/// <summary>
/// Enriches workflow data by calling an external service or lookup.
/// </summary>
public sealed class ContentEnricherStep : IStep
{
    private readonly Func<IWorkflowContext, Task> _enrichAction;

    /// <summary>
    /// Initializes a new instance of <see cref="ContentEnricherStep"/>.
    /// </summary>
    /// <param name="enrichAction">The async action that enriches the workflow context data.</param>
    /// <param name="name">Optional step name.</param>
    public ContentEnricherStep(Func<IWorkflowContext, Task> enrichAction, string? name = null)
    {
        _enrichAction = enrichAction ?? throw new ArgumentNullException(nameof(enrichAction));
        Name = name ?? "ContentEnricher";
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public Task ExecuteAsync(IWorkflowContext context) => _enrichAction(context);
}
