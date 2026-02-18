namespace WorkflowFramework.Extensions.Integration.Routing;

/// <summary>
/// Routes workflow execution using logic that can evolve based on previous results.
/// The routing function returns the next step to execute, or null to stop routing.
/// </summary>
public sealed class DynamicRouterStep : IStep
{
    private readonly Func<IWorkflowContext, IStep?> _routingFunction;

    /// <summary>
    /// Initializes a new instance of <see cref="DynamicRouterStep"/>.
    /// </summary>
    /// <param name="routingFunction">Function that determines the next step based on current context. Returns null to stop.</param>
    public DynamicRouterStep(Func<IWorkflowContext, IStep?> routingFunction)
    {
        _routingFunction = routingFunction ?? throw new ArgumentNullException(nameof(routingFunction));
    }

    /// <inheritdoc />
    public string Name => "DynamicRouter";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        IStep? nextStep;
        while ((nextStep = _routingFunction(context)) != null)
        {
            await nextStep.ExecuteAsync(context).ConfigureAwait(false);
            if (context.IsAborted) break;
        }
    }
}
