using WorkflowFramework.Extensions.Integration.Abstractions;

namespace WorkflowFramework.Extensions.Integration.Routing;

/// <summary>
/// Routes workflow execution to different branches based on message content using predicates.
/// </summary>
public sealed class ContentBasedRouterStep : IStep
{
    private readonly List<(Func<IWorkflowContext, bool> Predicate, IStep Step)> _routes;
    private readonly IStep? _defaultRoute;

    /// <summary>
    /// Initializes a new instance of <see cref="ContentBasedRouterStep"/>.
    /// </summary>
    /// <param name="routes">The routing predicates and their target steps.</param>
    /// <param name="defaultRoute">Optional default route when no predicate matches.</param>
    public ContentBasedRouterStep(
        IEnumerable<(Func<IWorkflowContext, bool> Predicate, IStep Step)> routes,
        IStep? defaultRoute = null)
    {
        _routes = routes?.ToList() ?? throw new ArgumentNullException(nameof(routes));
        _defaultRoute = defaultRoute;
    }

    /// <inheritdoc />
    public string Name => "ContentBasedRouter";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        foreach (var (predicate, step) in _routes)
        {
            if (predicate(context))
            {
                await step.ExecuteAsync(context).ConfigureAwait(false);
                return;
            }
        }

        if (_defaultRoute != null)
        {
            await _defaultRoute.ExecuteAsync(context).ConfigureAwait(false);
        }
    }
}
