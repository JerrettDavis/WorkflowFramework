using System.Diagnostics;

namespace WorkflowFramework.Extensions.Diagnostics;

/// <summary>
/// Middleware that collects workflow execution metrics.
/// </summary>
public sealed class MetricsMiddleware : IWorkflowMiddleware
{
    private long _totalSteps;
    private long _failedSteps;
    private long _totalElapsedTicks;

    /// <summary>Gets total step executions.</summary>
    public long TotalSteps => Interlocked.Read(ref _totalSteps);

    /// <summary>Gets failed step executions.</summary>
    public long FailedSteps => Interlocked.Read(ref _failedSteps);

    /// <summary>Gets average step duration.</summary>
    public TimeSpan AverageDuration
    {
        get
        {
            var total = TotalSteps;
            return total > 0
                ? TimeSpan.FromTicks(Interlocked.Read(ref _totalElapsedTicks) / total)
                : TimeSpan.Zero;
        }
    }

    /// <inheritdoc />
    public async Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch
        {
            Interlocked.Increment(ref _failedSteps);
            throw;
        }
        finally
        {
            sw.Stop();
            Interlocked.Increment(ref _totalSteps);
            Interlocked.Add(ref _totalElapsedTicks, sw.Elapsed.Ticks);
        }
    }
}
