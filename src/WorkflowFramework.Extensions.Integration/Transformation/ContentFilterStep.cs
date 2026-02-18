namespace WorkflowFramework.Extensions.Integration.Transformation;

/// <summary>
/// Strips unnecessary fields from workflow data, keeping only what downstream needs.
/// </summary>
public sealed class ContentFilterStep : IStep
{
    private readonly Func<IWorkflowContext, Task> _filterAction;

    /// <summary>
    /// Initializes a new instance of <see cref="ContentFilterStep"/>.
    /// </summary>
    /// <param name="filterAction">The action that filters/strips workflow context data.</param>
    /// <param name="name">Optional step name.</param>
    public ContentFilterStep(Func<IWorkflowContext, Task> filterAction, string? name = null)
    {
        _filterAction = filterAction ?? throw new ArgumentNullException(nameof(filterAction));
        Name = name ?? "ContentFilter";
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public Task ExecuteAsync(IWorkflowContext context) => _filterAction(context);
}
