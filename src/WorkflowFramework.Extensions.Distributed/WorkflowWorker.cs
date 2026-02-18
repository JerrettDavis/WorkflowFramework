namespace WorkflowFramework.Extensions.Distributed;

/// <summary>
/// A worker that pulls and executes workflow items from a queue.
/// </summary>
public sealed class WorkflowWorker : IAsyncDisposable
{
    private readonly IWorkflowQueue _queue;
    private readonly Func<WorkflowQueueItem, CancellationToken, Task> _handler;
    private readonly WorkflowWorkerOptions _options;
    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private long _processedCount;
    private long _failedCount;
    private int _running;

    /// <summary>
    /// Initializes a new instance of <see cref="WorkflowWorker"/>.
    /// </summary>
    /// <param name="queue">The queue to pull work from.</param>
    /// <param name="handler">The handler for processing queue items.</param>
    /// <param name="options">Worker options.</param>
    public WorkflowWorker(IWorkflowQueue queue, Func<WorkflowQueueItem, CancellationToken, Task> handler, WorkflowWorkerOptions? options = null)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _options = options ?? new WorkflowWorkerOptions();
    }

    /// <summary>Gets the worker ID.</summary>
    public string WorkerId { get; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets whether the worker is running.</summary>
    public bool IsRunning => _running == 1;

    /// <summary>Gets the total processed item count.</summary>
    public long ProcessedCount => Interlocked.Read(ref _processedCount);

    /// <summary>Gets the total failed item count.</summary>
    public long FailedCount => Interlocked.Read(ref _failedCount);

    /// <summary>Gets the worker health status.</summary>
    public WorkerHealthStatus GetHealthStatus()
    {
        return new WorkerHealthStatus
        {
            WorkerId = WorkerId,
            IsRunning = IsRunning,
            ProcessedCount = ProcessedCount,
            FailedCount = FailedCount,
            LastCheckTime = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Starts the worker.</summary>
    public void Start()
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0) return;
        _cts = new CancellationTokenSource();
        _runTask = RunAsync(_cts.Token);
    }

    /// <summary>Stops the worker.</summary>
    public async Task StopAsync()
    {
        if (Interlocked.CompareExchange(ref _running, 0, 1) != 1) return;
        _cts?.Cancel();
        if (_runTask != null)
        {
            try { await _runTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _cts?.Dispose();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var item = await _queue.DequeueAsync(ct).ConfigureAwait(false);
                if (item != null)
                {
                    try
                    {
                        await _handler(item, ct).ConfigureAwait(false);
                        Interlocked.Increment(ref _processedCount);
                    }
                    catch
                    {
                        Interlocked.Increment(ref _failedCount);
                    }
                }
                else
                {
                    await Task.Delay(_options.PollingInterval, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
        }
    }
}

/// <summary>
/// Options for configuring a workflow worker.
/// </summary>
public sealed class WorkflowWorkerOptions
{
    /// <summary>Gets or sets the polling interval when the queue is empty.</summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(500);
}

/// <summary>
/// Represents a worker's health status.
/// </summary>
public sealed class WorkerHealthStatus
{
    /// <summary>Gets or sets the worker ID.</summary>
    public string WorkerId { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the worker is running.</summary>
    public bool IsRunning { get; set; }

    /// <summary>Gets or sets the processed item count.</summary>
    public long ProcessedCount { get; set; }

    /// <summary>Gets or sets the failed item count.</summary>
    public long FailedCount { get; set; }

    /// <summary>Gets or sets the last check time.</summary>
    public DateTimeOffset LastCheckTime { get; set; }
}
