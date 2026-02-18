using System.Diagnostics;

namespace WorkflowFramework.Extensions.Diagnostics;

/// <summary>
/// Middleware that creates OpenTelemetry spans for each workflow step.
/// </summary>
public sealed class TracingMiddleware : IWorkflowMiddleware
{
    /// <inheritdoc />
    public async Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
    {
        using var activity = WorkflowActivitySource.Instance.StartActivity(
            $"workflow.step.{step.Name}",
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
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
            {
                { "exception.type", ex.GetType().FullName },
                { "exception.message", ex.Message }
            }));
            throw;
        }
    }
}
