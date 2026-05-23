using PatternKit.Messaging.Transformation;
using WorkflowFramework.Builder;
using WorkflowFramework.Extensions.Integration.Abstractions;
using WorkflowFramework.Extensions.Integration.Channel;
using WorkflowFramework.Extensions.Integration.Composition;
using WorkflowFramework.Extensions.Integration.Routing;
using WorkflowFramework.Extensions.Integration.Transformation;

namespace WorkflowFramework.Extensions.Integration.Builder;

/// <summary>
/// Fluent builder extensions for Enterprise Integration Pattern steps.
/// </summary>
public static class IntegrationBuilderExtensions
{
    /// <summary>
    /// Adds a content-based router step.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="routes">The routing predicates and their target steps.</param>
    /// <param name="defaultRoute">Optional default route.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder Route(
        this IWorkflowBuilder builder,
        IEnumerable<(Func<IWorkflowContext, bool> Predicate, IStep Step)> routes,
        IStep? defaultRoute = null)
    {
        return builder.Step(new ContentBasedRouterStep(routes, defaultRoute));
    }

    /// <summary>
    /// Adds a message filter step.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="predicate">Returns true to continue, false to drop.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder Filter(
        this IWorkflowBuilder builder,
        Func<IWorkflowContext, bool> predicate)
    {
        return builder.Step(new MessageFilterStep(predicate));
    }

    /// <summary>
    /// Adds a dynamic router step.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="routingFunction">Function that returns next step or null to stop.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder DynamicRoute(
        this IWorkflowBuilder builder,
        Func<IWorkflowContext, IStep?> routingFunction)
    {
        return builder.Step(new DynamicRouterStep(routingFunction));
    }

    /// <summary>
    /// Adds a recipient list step.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="recipientSelector">Function that determines recipients.</param>
    /// <param name="parallel">Whether to send in parallel.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder RecipientList(
        this IWorkflowBuilder builder,
        Func<IWorkflowContext, IEnumerable<IStep>> recipientSelector,
        bool parallel = false)
    {
        return builder.Step(new RecipientListStep(recipientSelector, parallel));
    }

    /// <summary>
    /// Adds a splitter step.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="splitter">Function to split data into items.</param>
    /// <param name="itemProcessor">Step to process each item.</param>
    /// <param name="parallel">Whether to process in parallel.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder Split(
        this IWorkflowBuilder builder,
        Func<IWorkflowContext, IEnumerable<object>> splitter,
        IStep itemProcessor,
        bool parallel = false)
    {
        return builder.Step(new SplitterStep(splitter, itemProcessor, parallel));
    }

    /// <summary>
    /// Adds an aggregator step.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="itemsSelector">Function to select items to aggregate.</param>
    /// <param name="aggregateAction">Action to aggregate items.</param>
    /// <param name="configure">Optional configuration for completion conditions.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder Aggregate(
        this IWorkflowBuilder builder,
        Func<IWorkflowContext, IEnumerable<object>> itemsSelector,
        Func<IReadOnlyList<object>, IWorkflowContext, Task> aggregateAction,
        Action<AggregatorOptions>? configure = null)
    {
        var options = new AggregatorOptions();
        configure?.Invoke(options);
        return builder.Step(new AggregatorStep(itemsSelector, aggregateAction, options));
    }

    /// <summary>
    /// Adds a scatter-gather step with typed recipients.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="recipients">The typed recipients to scatter to.</param>
    /// <param name="aggregator">Function to aggregate results.</param>
    /// <param name="timeout">Maximum wait time.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder ScatterGather(
        this IWorkflowBuilder builder,
        IEnumerable<ScatterGatherStep.Recipient> recipients,
        Func<IReadOnlyList<object?>, IWorkflowContext, Task> aggregator,
        TimeSpan timeout)
    {
        return builder.Step(new ScatterGatherStep(recipients, aggregator, timeout));
    }

    /// <summary>
    /// Adds a scatter-gather step with legacy IStep handlers (deprecated).
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="handlers">The handler steps to scatter to.</param>
    /// <param name="aggregator">Function to aggregate results.</param>
    /// <param name="timeout">Maximum wait time.</param>
    /// <returns>This builder for chaining.</returns>
    [Obsolete(
        "Use ScatterGather(IEnumerable<ScatterGatherStep.Recipient>, ...) instead. " +
        "The IEnumerable<IStep> overload is deprecated and will be removed in the next major version.",
        error: false)]
    public static IWorkflowBuilder ScatterGather(
        this IWorkflowBuilder builder,
        IEnumerable<IStep> handlers,
        Func<IReadOnlyList<object?>, IWorkflowContext, Task> aggregator,
        TimeSpan timeout)
    {
#pragma warning disable CS0618 // suppress deprecated ScatterGatherStep overload
        return builder.Step(new ScatterGatherStep(handlers, aggregator, timeout));
#pragma warning restore CS0618
    }

    /// <summary>
    /// Adds a content enricher step.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="enrichAction">The enrichment action.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder Enrich(
        this IWorkflowBuilder builder,
        Func<IWorkflowContext, Task> enrichAction)
    {
        return builder.Step(new ContentEnricherStep(enrichAction));
    }

    /// <summary>
    /// Adds a wire tap step for audit/debug.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="tapAction">The tap action (e.g., logging).</param>
    /// <param name="swallowErrors">Whether to swallow errors from tap. Default true.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder WireTap(
        this IWorkflowBuilder builder,
        Func<IWorkflowContext, Task> tapAction,
        bool swallowErrors = true)
    {
        return builder.Step(new WireTapStep(tapAction, swallowErrors));
    }

    /// <summary>
    /// Adds a dead letter step wrapping an inner step.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="store">The dead letter store.</param>
    /// <param name="innerStep">The step to wrap.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder WithDeadLetter(
        this IWorkflowBuilder builder,
        IDeadLetterStore store,
        IStep innerStep)
    {
        return builder.Step(new DeadLetterStep(store, innerStep));
    }

    /// <summary>
    /// Adds a claim check (store) step consuming
    /// <see cref="IClaimCheckStore{TPayload}">PatternKit IClaimCheckStore&lt;object&gt;</see>.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="store">The PatternKit typed claim check store.</param>
    /// <param name="payloadSelector">Function to select the payload to store.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder ClaimCheck(
        this IWorkflowBuilder builder,
        IClaimCheckStore<object> store,
        Func<IWorkflowContext, object> payloadSelector)
    {
        return builder.Step(new ClaimCheckStep(store, payloadSelector));
    }

    /// <summary>
    /// Adds a claim check (store) step using a legacy <see cref="WorkflowFramework.Extensions.Integration.Abstractions.IClaimCheckStore"/> (deprecated).
    /// </summary>
    [Obsolete(
        "Use ClaimCheck(IClaimCheckStore<object>, ...) instead. " +
        "The untyped WF IClaimCheckStore is obsolete. Wrap with LegacyClaimCheckStoreAdapter for one release.",
        error: false)]
    public static IWorkflowBuilder ClaimCheck(
        this IWorkflowBuilder builder,
#pragma warning disable CS0618
        WorkflowFramework.Extensions.Integration.Abstractions.IClaimCheckStore store,
#pragma warning restore CS0618
        Func<IWorkflowContext, object> payloadSelector)
    {
#pragma warning disable CS0618
        var adapter = new LegacyClaimCheckStoreAdapter(store);
#pragma warning restore CS0618
        return builder.Step(new ClaimCheckStep(adapter, payloadSelector));
    }

    /// <summary>
    /// Adds a claim retrieve step consuming
    /// <see cref="IClaimCheckStore{TPayload}">PatternKit IClaimCheckStore&lt;object&gt;</see>.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="store">The PatternKit typed claim check store.</param>
    /// <param name="resultKey">Property key for retrieved payload.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder ClaimRetrieve(
        this IWorkflowBuilder builder,
        IClaimCheckStore<object> store,
        string resultKey = "__ClaimPayload")
    {
        return builder.Step(new ClaimRetrieveStep(store, resultKey));
    }

    /// <summary>
    /// Adds a claim retrieve step using a legacy <see cref="WorkflowFramework.Extensions.Integration.Abstractions.IClaimCheckStore"/> (deprecated).
    /// </summary>
    [Obsolete(
        "Use ClaimRetrieve(IClaimCheckStore<object>, ...) instead. " +
        "The untyped WF IClaimCheckStore is obsolete. Wrap with LegacyClaimCheckStoreAdapter for one release.",
        error: false)]
    public static IWorkflowBuilder ClaimRetrieve(
        this IWorkflowBuilder builder,
#pragma warning disable CS0618
        WorkflowFramework.Extensions.Integration.Abstractions.IClaimCheckStore store,
#pragma warning restore CS0618
        string resultKey = "__ClaimPayload")
    {
#pragma warning disable CS0618
        var adapter = new LegacyClaimCheckStoreAdapter(store);
#pragma warning restore CS0618
        return builder.Step(new ClaimRetrieveStep(adapter, resultKey));
    }

    /// <summary>
    /// Adds a resequencer step.
    /// </summary>
    /// <param name="builder">The workflow builder.</param>
    /// <param name="itemsSelector">Function to select items to reorder.</param>
    /// <param name="sequenceSelector">Function to extract sequence number.</param>
    /// <returns>This builder for chaining.</returns>
    public static IWorkflowBuilder Resequence(
        this IWorkflowBuilder builder,
        Func<IWorkflowContext, IEnumerable<object>> itemsSelector,
        Func<object, long> sequenceSelector)
    {
        return builder.Step(new ResequencerStep(itemsSelector, sequenceSelector));
    }
}
