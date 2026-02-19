using System.Text;
using WorkflowFramework.Samples.TaskStream.Models;

namespace WorkflowFramework.Samples.TaskStream.Steps;

/// <summary>
/// Renders the <see cref="TaskStreamResult"/> as a clean markdown report.
/// </summary>
public sealed class FormatMarkdownStep : IStep
{
    /// <inheritdoc />
    public string Name => "FormatMarkdown";

    /// <inheritdoc />
    public Task ExecuteAsync(IWorkflowContext context)
    {
        var result = (TaskStreamResult)context.Properties["taskStreamResult"]!;
        var sb = new StringBuilder();

        sb.AppendLine("# 📋 TaskStream Report");
        sb.AppendLine();
        sb.AppendLine("## Statistics");
        sb.AppendLine($"- **Messages processed:** {result.Stats.TotalMessages}");
        sb.AppendLine($"- **Tasks extracted:** {result.Stats.TotalTasks}");
        sb.AppendLine($"- **Automated:** {result.Stats.AutomatedCount}");
        sb.AppendLine($"- **Human-required:** {result.Stats.HumanCount}");
        sb.AppendLine($"- **Duplicates removed:** {result.Stats.DuplicatesRemoved}");
        sb.AppendLine();

        if (result.AutomatedResults.Count > 0)
        {
            sb.AppendLine("## 🤖 Automated Tasks");
            sb.AppendLine();
            foreach (var ar in result.AutomatedResults)
            {
                sb.AppendLine($"### ✅ {ar.Task.Title}");
                sb.AppendLine($"- **Status:** Completed");
                sb.AppendLine($"- **Result:** {ar.Result}");
                sb.AppendLine();
            }
        }

        if (result.EnrichedItems.Count > 0)
        {
            sb.AppendLine("## 👤 Human Tasks");
            sb.AppendLine();
            foreach (var item in result.EnrichedItems)
            {
                sb.AppendLine($"### 📌 {item.Title}");
                sb.AppendLine($"- **Priority:** {item.Priority}");
                sb.AppendLine($"- **Category:** {item.Category}");
                foreach (var (key, value) in item.Enrichments)
                    sb.AppendLine($"- **{key}:** {value}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("---");
        sb.AppendLine($"*Generated at {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC by TaskStream*");

        var report = sb.ToString();
        result.MarkdownReport = report;
        context.Properties["markdownReport"] = report;
        Console.WriteLine("  📝 Markdown report generated");
        return Task.CompletedTask;
    }
}
