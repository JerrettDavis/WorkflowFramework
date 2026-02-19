using WorkflowFramework.Samples.TaskStream.Steps;

namespace WorkflowFramework.Samples.TaskStream.Workflows;

/// <summary>
/// Workflow: AggregateResults → FormatMarkdown
/// </summary>
public static class ReportWorkflow
{
    /// <summary>Builds the report workflow.</summary>
    public static IWorkflow Build(
        AggregateResultsStep aggregate,
        FormatMarkdownStep format)
    {
        return Workflow.Create("Report")
            .Step(aggregate)
            .Step(format)
            .Build();
    }
}
