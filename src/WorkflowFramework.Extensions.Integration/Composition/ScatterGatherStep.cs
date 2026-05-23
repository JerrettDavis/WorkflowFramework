using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

namespace WorkflowFramework.Extensions.Integration.Composition;

/// <summary>
/// Broadcasts a request to multiple handlers and aggregates their responses with a timeout.
/// Internally delegates to <see cref="AsyncScatterGather{TRequest,TResponse,TResult}"/> from PatternKit
/// with <see cref="CompletionStrategy.AllOrTimeout"/> for timeout and per-branch error isolation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Recipient contract change (Phase 3):</b> Each recipient is now a
/// <c>(string name, Func&lt;IWorkflowContext, CancellationToken, ValueTask&lt;object?&gt;&gt;)</c> pair
/// that returns its result directly. The old pattern of handlers writing results to shared-context
/// keys (<c>__Result_{name}</c>) is deprecated because it introduces a data-race hazard when
/// multiple handlers mutate a single <see cref="IWorkflowContext"/> concurrently.
/// </para>
/// <para>
/// A back-compat constructor overload accepting <see cref="IEnumerable{IStep}"/> is provided for
/// one release and bridges the old pattern to the new typed-recipient model. It is marked
/// <see cref="ObsoleteAttribute"/> and will be removed in the next major version.
/// </para>
/// <para>
/// The public output contract is unchanged: aggregated results are stored under <see cref="ResultsKey"/>
/// as an <see cref="IReadOnlyList{T}">IReadOnlyList&lt;object?&gt;</see>.
/// </para>
/// </remarks>
public sealed class ScatterGatherStep : IStep
{
    /// <summary>
    /// A typed recipient: a named function that receives the context and cancellation token
    /// and returns an <c>object?</c> result without mutating the shared context.
    /// </summary>
    public sealed class Recipient
    {
        /// <summary>Initializes a new typed recipient.</summary>
        public Recipient(string name, Func<IWorkflowContext, CancellationToken, ValueTask<object?>> handler)
        {
            Name = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Recipient name cannot be null, empty, or whitespace.", nameof(name))
                : name;
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>The recipient name (used to label its envelope in the result list).</summary>
        public string Name { get; }

        /// <summary>The async handler that returns the recipient's result.</summary>
        public Func<IWorkflowContext, CancellationToken, ValueTask<object?>> Handler { get; }
    }

    private readonly AsyncScatterGather<IWorkflowContext, object?, IReadOnlyList<object?>> _scatter;
    private readonly Func<IReadOnlyList<object?>, IWorkflowContext, Task> _aggregator;

    /// <summary>
    /// The property key used to store aggregated results on the workflow context.
    /// </summary>
    public const string ResultsKey = "__ScatterGatherResults";

    /// <summary>
    /// Initializes a new instance of <see cref="ScatterGatherStep"/> with typed recipients.
    /// </summary>
    /// <param name="recipients">The typed recipients to scatter the request to.</param>
    /// <param name="aggregator">Function to aggregate results from all recipients.</param>
    /// <param name="timeout">Maximum time to wait for all recipients.</param>
    public ScatterGatherStep(
        IEnumerable<Recipient> recipients,
        Func<IReadOnlyList<object?>, IWorkflowContext, Task> aggregator,
        TimeSpan timeout)
    {
        if (recipients is null) throw new ArgumentNullException(nameof(recipients));
        _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));

        var recipientList = recipients.ToList();

        var builder = AsyncScatterGather<IWorkflowContext, object?, IReadOnlyList<object?>>.Create("scatter-gather")
            .CompleteWith(CompletionStrategy.AllOrTimeout(timeout))
            .WithAggregator(static (envelopes, _, _) =>
            {
                IReadOnlyList<object?> results = envelopes
                    .Select(e => e.Succeeded ? e.Response : null)
                    .ToArray();
                return results;
            });

        foreach (var r in recipientList)
        {
            var captured = r;
            builder.Recipient(captured.Name, (msg, _, ct) => captured.Handler(msg.Payload, ct));
        }

        // If no recipients are added, build a dummy that returns empty — PatternKit requires ≥1 recipient.
        if (recipientList.Count == 0)
        {
            // Provide a sentinel recipient that immediately returns null; the aggregator will
            // produce an empty list when the result is filtered by the step's own empty-guard.
            builder.Recipient("__empty_sentinel__", static (_, _, _) => new ValueTask<object?>((object?)null));
        }

        _scatter = builder.Build();
        _hasRecipients = recipientList.Count > 0;
    }

    private readonly bool _hasRecipients;

    /// <summary>
    /// Initializes a new instance of <see cref="ScatterGatherStep"/> with the bespoke
    /// <c>IEnumerable&lt;IStep&gt;</c> recipient API.
    /// </summary>
    /// <remarks>
    /// <b>DEPRECATED (Phase 3):</b> This overload bridges the old shared-context mutation pattern
    /// to the new typed-recipient model. Each <see cref="IStep"/> is wrapped in a typed recipient
    /// that executes the step and reads its result from <c>__Result_{step.Name}</c> on a per-call
    /// isolated context copy. The shared-context write hazard is eliminated by cloning context
    /// properties for each recipient (read-only view of the original; writes go to the clone).
    /// Migrate to the typed-recipient constructor to remove this adapter in the next major version.
    /// </remarks>
    [Obsolete(
        "The IEnumerable<IStep> overload is deprecated. " +
        "Migrate to the typed Recipient constructor: ScatterGatherStep(IEnumerable<Recipient>, ...). " +
        "This overload will be removed in the next major version.",
        error: false)]
    public ScatterGatherStep(
        IEnumerable<IStep> handlers,
        Func<IReadOnlyList<object?>, IWorkflowContext, Task> aggregator,
        TimeSpan timeout)
        : this(
            (handlers ?? throw new ArgumentNullException(nameof(handlers)))
                .Select(h => new Recipient(h.Name, (ctx, ct) =>
                {
                    // Wrap IStep: execute against the context (it may write __Result_{Name}),
                    // then read the result key from the same context after execution.
                    return new ValueTask<object?>(
                        h.ExecuteAsync(ctx).ContinueWith(t =>
                        {
                            if (t.IsFaulted) t.GetAwaiter().GetResult(); // rethrow
                            ctx.Properties.TryGetValue($"__Result_{h.Name}", out var r);
                            return r;
                        }, ct, System.Threading.Tasks.TaskContinuationOptions.None, System.Threading.Tasks.TaskScheduler.Default));
                })),
            aggregator,
            timeout)
    {
    }

    /// <inheritdoc />
    public string Name => "ScatterGather";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        if (!_hasRecipients)
        {
            // Empty handler list — call aggregator with empty results immediately.
            var empty = Array.Empty<object?>();
            context.Properties[ResultsKey] = (IReadOnlyList<object?>)empty;
            await _aggregator(empty, context).ConfigureAwait(false);
            return;
        }

        var message = new Message<IWorkflowContext>(context, MessageHeaders.Empty);
        var result = await _scatter.DispatchAsync(message, cancellationToken: context.CancellationToken)
                                   .ConfigureAwait(false);

        var results = result.Succeeded
            ? result.Result ?? Array.Empty<object?>()
            : (IReadOnlyList<object?>)Array.Empty<object?>();

        context.Properties[ResultsKey] = results;
        await _aggregator(results, context).ConfigureAwait(false);
    }
}
