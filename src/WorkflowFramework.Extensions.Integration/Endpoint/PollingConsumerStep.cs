using PatternKit.Messaging;
using PatternKit.Messaging.Consumers;
using WorkflowFramework.Extensions.Integration.Abstractions;

namespace WorkflowFramework.Extensions.Integration.Endpoint;

/// <summary>
/// Periodically polls an external source for new data.
/// Delegates the single-shot poll to <see cref="AsyncPollingConsumer{TPayload}.PollOnceAsync"/>.
/// </summary>
/// <typeparam name="T">The type of data items.</typeparam>
public sealed class PollingConsumerStep<T> : IStep
{
    private readonly AsyncPollingConsumer<IReadOnlyList<T>> _consumer;

    /// <summary>
    /// The property key used to store polled items.
    /// </summary>
    public const string ResultKey = "__PolledItems";

    /// <summary>
    /// Initializes a new instance of <see cref="PollingConsumerStep{T}"/>.
    /// </summary>
    /// <param name="source">The polling source.</param>
    public PollingConsumerStep(IPollingSource<T> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        _consumer = AsyncPollingConsumer<IReadOnlyList<T>>.Create("PollingConsumer")
            .WithSource(async (_, ct) =>
            {
                var items = await source.PollAsync(ct).ConfigureAwait(false);
                return new Message<IReadOnlyList<T>>(items);
            })
            .Build();
    }

    /// <inheritdoc />
    public string Name => "PollingConsumer";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var message = await _consumer.PollOnceAsync(ct: context.CancellationToken)
            .ConfigureAwait(false);

        // PollOnceAsync returns null only when the source delegate returns null.
        // Our delegate always returns a Message (wrapping an empty list when there are no items),
        // so message will not be null in practice. The null-coalescing guard is defensive.
        context.Properties[ResultKey] = message?.Payload ?? (IReadOnlyList<T>)Array.Empty<T>();
    }
}
