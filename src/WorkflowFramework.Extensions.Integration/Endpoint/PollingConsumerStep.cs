using WorkflowFramework.Extensions.Integration.Abstractions;

namespace WorkflowFramework.Extensions.Integration.Endpoint;

/// <summary>
/// Periodically polls an external source for new data.
/// </summary>
/// <typeparam name="T">The type of data items.</typeparam>
public sealed class PollingConsumerStep<T> : IStep
{
    private readonly IPollingSource<T> _source;
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
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <inheritdoc />
    public string Name => "PollingConsumer";

    /// <inheritdoc />
    public async Task ExecuteAsync(IWorkflowContext context)
    {
        var items = await _source.PollAsync(context.CancellationToken).ConfigureAwait(false);
        context.Properties[ResultKey] = items;
    }
}
