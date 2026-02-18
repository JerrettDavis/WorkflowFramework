using System.Diagnostics;

namespace WorkflowFramework.Extensions.Diagnostics;

/// <summary>
/// Middleware that measures and records step execution time.
/// </summary>
public sealed class TimingMiddleware : IWorkflowMiddleware
{
    /// <summary>
    /// The property key used to store timing results in the context.
    /// </summary>
    public const string TimingsKey = "WorkflowFramework.StepTimings";

    /// <inheritdoc />
    public async Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            await next(context).ConfigureAwait(false);
        }
        finally
        {
            sw.Stop();
            RecordTiming(context, step.Name, sw.Elapsed);
        }
    }

    private static void RecordTiming(IWorkflowContext context, string stepName, TimeSpan elapsed)
    {
        if (!context.Properties.TryGetValue(TimingsKey, out var existing) || existing is not Dictionary<string, TimeSpan> timings)
        {
            timings = new Dictionary<string, TimeSpan>();
            context.Properties[TimingsKey] = timings;
        }

        timings[stepName] = elapsed;
    }
}
