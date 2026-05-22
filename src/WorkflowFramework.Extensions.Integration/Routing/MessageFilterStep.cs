// Intentionally bespoke — MessageFilterStep is a one-line Specification pattern
// (predicate → abort). PatternKit.Core 0.105.0 does not expose a standalone Specification
// type; the Behavioral.Strategy primitives are overkill for a single boolean predicate.
// The implementation is kept as-is to avoid unnecessary indirection. Characterization
// tests added in Phase G.1.
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
