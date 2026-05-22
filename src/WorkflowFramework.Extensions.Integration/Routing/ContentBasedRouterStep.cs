// Internal implementation uses PatternKit.Behavioral.Strategy.AsyncActionStrategy<IWorkflowContext>
// (PatternKit 0.105.0) to evaluate the predicate/handler branches in order.
// Public API is identical to the original bespoke implementation.
using PatternKit.Behavioral.Strategy;
using WorkflowFramework.Extensions.Integration.Abstractions;

namespace WorkflowFramework.Extensions.Integration.Routing;

/// <summary>
/// Routes workflow execution to different branches based on message content using predicates.
/// </summary>
public sealed class ContentBasedRouterStep : IStep
{
    private readonly AsyncActionStrategy<IWorkflowContext> _strategy;

    /// <summary>
    /// Initializes a new instance of <see cref="ContentBasedRouterStep"/>.
    /// </summary>
    /// <param name="routes">The routing predicates and their target steps.</param>
    /// <param name="defaultRoute">Optional default route when no predicate matches.</param>
    public ContentBasedRouterStep(
        IEnumerable<(Func<IWorkflowContext, bool> Predicate, IStep Step)> routes,
        IStep? defaultRoute = null)
    {
        if (routes is null) throw new ArgumentNullException(nameof(routes));

        var builder = AsyncActionStrategy<IWorkflowContext>.Create();

        foreach (var (predicate, step) in routes)
        {
            var capturedStep = step;
            var capturedPredicate = predicate;
            builder
                .When(ctx => capturedPredicate(ctx))
                .Then(async (ctx, ct) => await capturedStep.ExecuteAsync(ctx).ConfigureAwait(false));
        }

        if (defaultRoute != null)
        {
            var capturedDefault = defaultRoute;
            builder.Default(async (ctx, ct) => await capturedDefault.ExecuteAsync(ctx).ConfigureAwait(false));
        }
        else
        {
            // No default — silently do nothing when no predicate matches
            builder.Default(_ => { });
        }

        _strategy = builder.Build();
    }

    /// <inheritdoc />
    public string Name => "ContentBasedRouter";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        await _strategy.ExecuteAsync(context, context.CancellationToken).ConfigureAwait(false);
    }
}
