using System.Collections.Concurrent;

namespace WorkflowFramework.Extensions.Events;

/// <summary>
/// In-memory implementation of <see cref="IEventBus"/>.
/// </summary>
public sealed class InMemoryEventBus : IEventBus
{
    private readonly ConcurrentDictionary<string, List<Func<WorkflowEvent, Task>>> _subscribers = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<WorkflowEvent>> _waiters = new();
    private readonly ConcurrentQueue<WorkflowEvent> _deadLetters = new();

    /// <summary>Gets events that could not be delivered.</summary>
    public IReadOnlyCollection<WorkflowEvent> DeadLetters
    {
        get
        {
            return _deadLetters.ToArray();
        }
    }

    /// <inheritdoc />
    public async Task PublishAsync(WorkflowEvent evt, CancellationToken cancellationToken = default)
    {
        if (evt == null) throw new ArgumentNullException(nameof(evt));

        // Check for correlated waiters
        var waiterKey = $"{evt.EventType}:{evt.CorrelationId}";
        if (_waiters.TryRemove(waiterKey, out var tcs))
        {
            tcs.TrySetResult(evt);
            return;
        }

        // Deliver to subscribers
        var delivered = false;
        if (_subscribers.TryGetValue(evt.EventType, out var handlers))
        {
            List<Func<WorkflowEvent, Task>> snapshot;
            lock (handlers)
            {
                snapshot = new List<Func<WorkflowEvent, Task>>(handlers);
            }
            foreach (var handler in snapshot)
            {
                await handler(evt).ConfigureAwait(false);
                delivered = true;
            }
        }

        if (!delivered)
        {
            _deadLetters.Enqueue(evt);
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(string eventType, Func<WorkflowEvent, Task> handler)
    {
        var handlers = _subscribers.GetOrAdd(eventType, _ => new List<Func<WorkflowEvent, Task>>());
        lock (handlers)
        {
            handlers.Add(handler);
        }
        return new Subscription(() =>
        {
            lock (handlers)
            {
                handlers.Remove(handler);
            }
        });
    }

    /// <inheritdoc />
    public async Task<WorkflowEvent?> WaitForEventAsync(string eventType, string correlationId, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var key = $"{eventType}:{correlationId}";
        var tcs = new TaskCompletionSource<WorkflowEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        _waiters[key] = tcs;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            using (cts.Token.Register(() => tcs.TrySetCanceled()))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }
        catch (TaskCanceledException)
        {
            _waiters.TryRemove(key, out _);
            return null;
        }
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        public void Dispose() => unsubscribe();
    }
}
