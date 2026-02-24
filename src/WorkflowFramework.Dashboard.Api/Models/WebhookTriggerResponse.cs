namespace WorkflowFramework.Dashboard.Api.Models;

/// <summary>
/// Response from a webhook trigger.
/// </summary>
public sealed class WebhookTriggerResponse
{
    public string RunId { get; set; } = "";
    public string Status { get; set; } = "";
    public string WebhookId { get; set; } = "";
}

/// <summary>
/// Request to set a cron schedule on a workflow.
/// </summary>
public sealed class SetScheduleRequest
{
    public string CronExpression { get; set; } = "";
    public bool Enabled { get; set; } = true;
}
