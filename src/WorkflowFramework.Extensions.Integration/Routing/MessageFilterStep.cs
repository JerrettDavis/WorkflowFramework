namespace WorkflowFramework.Extensions.Integration.Routing;

/// <summary>
/// Filters messages that don't match criteria by aborting the workflow for non-matching items.
/// </summary>
public sealed class MessageFilterStep : IStep
{
    private readonly Func<IWorkflowContext, bool> _predicate;

    /// <summary>
    /// Initializes a new instance of <see cref="MessageFilterStep"/>.
    /// </summary>
    /// <param name="predicate">Returns true if the message should continue; false to drop it.</param>
    public MessageFilterStep(Func<IWorkflowContext, bool> predicate)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    /// <inheritdoc />
    public string Name => "MessageFilter";

    /// <inheritdoc />
    public Task ExecuteAsync(IWorkflowContext context)
    {
        if (!_predicate(context))
        {
            context.IsAborted = true;
        }

        return Task.CompletedTask;
    }
}
