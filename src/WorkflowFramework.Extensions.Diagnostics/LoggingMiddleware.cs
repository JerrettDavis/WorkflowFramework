using Microsoft.Extensions.Logging;

namespace WorkflowFramework.Extensions.Diagnostics;

/// <summary>
/// Middleware that logs step execution using structured logging via ILogger.
/// </summary>
public sealed class LoggingMiddleware : IWorkflowMiddleware
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="LoggingMiddleware"/>.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public LoggingMiddleware(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
    {
        _logger.LogInformation(
            "Step {StepName} starting. WorkflowId={WorkflowId}, CorrelationId={CorrelationId}",
            step.Name, context.WorkflowId, context.CorrelationId);

        try
        {
            await next(context).ConfigureAwait(false);

            _logger.LogInformation(
                "Step {StepName} completed. WorkflowId={WorkflowId}",
                step.Name, context.WorkflowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Step {StepName} failed. WorkflowId={WorkflowId}",
                step.Name, context.WorkflowId);
            throw;
        }
    }
}
