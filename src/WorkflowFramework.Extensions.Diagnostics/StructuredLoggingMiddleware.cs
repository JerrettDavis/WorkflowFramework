using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace WorkflowFramework.Extensions.Diagnostics;

/// <summary>
/// Middleware that emits structured log entries with correlation for each step.
/// </summary>
public sealed class StructuredLoggingMiddleware : IWorkflowMiddleware
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="StructuredLoggingMiddleware"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public StructuredLoggingMiddleware(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["WorkflowId"] = context.WorkflowId,
            ["CorrelationId"] = context.CorrelationId,
            ["StepName"] = step.Name,
            ["StepIndex"] = context.CurrentStepIndex
        });

        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Step {StepName} starting (workflow={WorkflowId})", step.Name, context.WorkflowId);

        try
        {
            await next(context).ConfigureAwait(false);
            sw.Stop();
            _logger.LogInformation("Step {StepName} completed in {ElapsedMs}ms", step.Name, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Step {StepName} failed after {ElapsedMs}ms", step.Name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
