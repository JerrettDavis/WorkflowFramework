namespace WorkflowFramework.Extensions.Diagnostics.ExecutionHistory;

/// <summary>
/// Middleware that automatically captures step execution data and records it to an
/// <see cref="IExecutionHistoryStore"/>. This middleware tracks per-step timing and
/// assembles a complete <see cref="WorkflowRunRecord"/> stored as a context property,
/// which is flushed to the store via <see cref="ExecutionHistoryEvents"/>.
/// </summary>
public sealed class ExecutionHistoryMiddleware : IWorkflowMiddleware
{
    /// <summary>
    /// The property key used to store the in-progress run record on the context.
    /// </summary>
    public const string RunRecordKey = "WorkflowFramework.ExecutionHistory.RunRecord";

    /// <inheritdoc />
    public async Task InvokeAsync(IWorkflowContext context, IStep step, StepDelegate next)
    {
        EnsureRunRecord(context);

        var stepRecord = new StepRunRecord
        {
            StepName = step.Name,
            StartedAt = DateTimeOffset.UtcNow,
            InputSnapshot = SnapshotProperties(context)
        };

        try
        {
            await next(context).ConfigureAwait(false);
            stepRecord.Status = WorkflowStatus.Completed;
        }
        catch (Exception ex)
        {
            stepRecord.Status = WorkflowStatus.Faulted;
            stepRecord.Error = ex.Message;
            throw;
        }
        finally
        {
            stepRecord.CompletedAt = DateTimeOffset.UtcNow;
            stepRecord.OutputSnapshot = SnapshotProperties(context);

            var runRecord = (WorkflowRunRecord)context.Properties[RunRecordKey]!;
            runRecord.StepResults.Add(stepRecord);
        }
    }

    private static void EnsureRunRecord(IWorkflowContext context)
    {
        if (context.Properties.ContainsKey(RunRecordKey))
            return;

        var record = new WorkflowRunRecord
        {
            RunId = context.WorkflowId,
            StartedAt = DateTimeOffset.UtcNow
        };
        context.Properties[RunRecordKey] = record;
    }

    private static Dictionary<string, object?> SnapshotProperties(IWorkflowContext context)
    {
        var snapshot = new Dictionary<string, object?>(context.Properties.Count);
        foreach (var kvp in context.Properties)
        {
            if (kvp.Key == RunRecordKey) continue;
            snapshot[kvp.Key] = kvp.Value;
        }
        return snapshot;
    }
}
