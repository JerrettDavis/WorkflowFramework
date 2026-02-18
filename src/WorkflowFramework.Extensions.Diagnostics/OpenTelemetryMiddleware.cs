using System.Diagnostics;

namespace WorkflowFramework.Extensions.Diagnostics;

/// <summary>
/// Middleware that creates OpenTelemetry spans for each workflow step execution.
/// </summary>
public sealed class OpenTelemetryMiddleware : IWorkflowMiddleware
{
    /// <inheritdoc />
    public async Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
    {
        using var activity = WorkflowActivitySource.Instance.StartActivity(
            $"Step:{step.Name}",
            ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("workflow.id", context.WorkflowId);
            activity.SetTag("workflow.correlation_id", context.CorrelationId);
            activity.SetTag("workflow.step.name", step.Name);
            activity.SetTag("workflow.step.index", context.CurrentStepIndex);
        }

        try
        {
            await next(context).ConfigureAwait(false);
            activity?.SetTag("workflow.step.status", "completed");
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("workflow.step.status", "failed");
            throw;
        }
    }
}
