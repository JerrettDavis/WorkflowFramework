using System.Globalization;
using System.Text.RegularExpressions;
using WorkflowFramework.Extensions.AI;
using WorkflowFramework.Samples.TaskStream.Models;
using WorkflowFramework.Samples.TaskStream.Tools;

namespace WorkflowFramework.Samples.TaskStream.Agents;

/// <summary>
/// Rule-based mock implementation of <see cref="IAgentProvider"/> that demonstrates
/// the extraction, triage, execution, and enrichment patterns without requiring
/// a real LLM connection.
/// </summary>
public sealed partial class TaskStreamAgentProvider : IAgentProvider
{
    private readonly IReadOnlyDictionary<string, IAgentTool> _tools;

    /// <summary>Initializes a new instance with the given tools.</summary>
    public TaskStreamAgentProvider(IEnumerable<IAgentTool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name);
    }

    /// <inheritdoc />
    public string Name => "TaskStream-RuleBased";

    /// <inheritdoc />
    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken = default)
    {
        var prompt = request.Prompt.ToLowerInvariant();

        // Route based on the prompt type
        string content;
        if (prompt.Contains("extract"))
            content = ExtractTasks(request);
        else if (prompt.Contains("enrich"))
            content = EnrichTask(request);
        else if (prompt.Contains("execute") || prompt.Contains("automate"))
            content = ExecuteTask(request);
        else
            content = $"Processed: {request.Prompt[..Math.Min(100, request.Prompt.Length)]}";

        return Task.FromResult(new LlmResponse
        {
            Content = content,
            FinishReason = "stop",
            Usage = new TokenUsage { PromptTokens = 50, CompletionTokens = 30, TotalTokens = 80 }
        });
    }

    /// <inheritdoc />
    public Task<string> DecideAsync(AgentDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var prompt = request.Prompt.ToLowerInvariant();

        // Triage: classify as automatable or human-required
        if (prompt.Contains("triage") || prompt.Contains("classify"))
        {
            var taskTitle = request.Variables.TryGetValue("taskTitle", out var t) ? t?.ToString() ?? "" : "";
            var category = ClassifyTask(taskTitle);
            return Task.FromResult(category.ToString());
        }

        // Default: pick first option
        return Task.FromResult(request.Options.FirstOrDefault() ?? "unknown");
    }

    /// <summary>
    /// Extracts individual task strings from a multi-task message.
    /// Returns newline-separated task titles.
    /// </summary>
    private static string ExtractTasks(LlmRequest request)
    {
        var rawContent = request.Variables.TryGetValue("content", out var c) ? c?.ToString() ?? "" : "";
        var tasks = SplitIntoTasks(rawContent);
        return string.Join("\n", tasks);
    }

    /// <summary>
    /// Splits raw text into individual task phrases.
    /// </summary>
    internal static List<string> SplitIntoTasks(string text)
    {
        // Split on commas, "and", periods, semicolons
        var parts = SplitPattern().Split(text)
            .Select(p => p.Trim())
            .Where(p => p.Length > 3)
            .Where(p => ActionVerbs().Any(v => p.Contains(v, StringComparison.OrdinalIgnoreCase)))
            .Select(CapitalizeFirst)
            .ToList();

        return parts.Count > 0 ? parts : [CapitalizeFirst(text.Trim())];
    }

    /// <summary>
    /// Classifies a task as automatable or human-required based on keywords.
    /// </summary>
    internal static TaskCategory ClassifyTask(string title)
    {
        var lower = title.ToLowerInvariant();

        string[] automatableKeywords =
            ["deploy", "send", "schedule", "update ci", "update pipeline", "email", "notify", "publish", "merge", "trigger"];
        string[] humanKeywords =
            ["buy", "pick up", "call", "meet", "visit", "physical", "birthday", "present", "milk", "grocery"];

        var autoScore = automatableKeywords.Count(k => lower.Contains(k));
        var humanScore = humanKeywords.Count(k => lower.Contains(k));

        if (autoScore > humanScore) return TaskCategory.Automatable;
        if (humanScore > autoScore) return TaskCategory.HumanRequired;
        return autoScore > 0 ? TaskCategory.Hybrid : TaskCategory.HumanRequired;
    }

    private string ExecuteTask(LlmRequest request)
    {
        var taskTitle = request.Variables.TryGetValue("taskTitle", out var t) ? t?.ToString() ?? "" : "";
        var lower = taskTitle.ToLowerInvariant();

        // Route to the appropriate tool
        if ((lower.Contains("deploy") || lower.Contains("ci") || lower.Contains("pipeline")) && _tools.TryGetValue("deployment", out var deployTool))
            return deployTool.ExecuteAsync(taskTitle).GetAwaiter().GetResult();
        if ((lower.Contains("schedule") || lower.Contains("meeting") || lower.Contains("standup")) && _tools.TryGetValue("calendar", out var calTool))
            return calTool.ExecuteAsync(taskTitle).GetAwaiter().GetResult();
        if ((lower.Contains("send") || lower.Contains("report") || lower.Contains("email")) && _tools.TryGetValue("filesystem", out var fsTool))
            return fsTool.ExecuteAsync(taskTitle).GetAwaiter().GetResult();
        if (_tools.TryGetValue("web_search", out var searchTool))
            return searchTool.ExecuteAsync(taskTitle).GetAwaiter().GetResult();

        return $"✅ Task completed: {taskTitle}";
    }

    private static string EnrichTask(LlmRequest request)
    {
        var taskTitle = request.Variables.TryGetValue("taskTitle", out var t) ? t?.ToString() ?? "" : "";
        var lower = taskTitle.ToLowerInvariant();

        var suggestions = new List<string>();

        if (lower.Contains("buy") || lower.Contains("pick up") || lower.Contains("milk") || lower.Contains("grocery"))
        {
            suggestions.Add("suggestion:Nearby stores — Target (0.8 mi), Walmart (1.2 mi)");
            suggestions.Add("timeEstimate:30 minutes");
            suggestions.Add("priority:2");
        }
        else if (lower.Contains("birthday") || lower.Contains("present"))
        {
            suggestions.Add("suggestion:Popular gift ideas on Amazon, local boutique 0.5 mi away");
            suggestions.Add("timeEstimate:1 hour");
            suggestions.Add("priority:3");
        }
        else if (lower.Contains("review") || lower.Contains("read") || lower.Contains("feedback"))
        {
            suggestions.Add("suggestion:Block 45 min of focus time on calendar");
            suggestions.Add("timeEstimate:45 minutes");
            suggestions.Add("priority:2");
        }
        else
        {
            suggestions.Add("suggestion:Consider delegating or scheduling for a low-energy time block");
            suggestions.Add("timeEstimate:20 minutes");
            suggestions.Add("priority:1");
        }

        return string.Join("\n", suggestions);
    }

    private static string CapitalizeFirst(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], CultureInfo.InvariantCulture) + s[1..];

    private static string[] ActionVerbs() =>
        ["schedule", "pick up", "deploy", "review", "send", "buy", "update", "book", "create",
         "merge", "call", "remind", "check", "fix", "build", "test", "write", "read", "meet"];

    [GeneratedRegex(@"\s*[,;]\s*|\s+and\s+|\.\s+", RegexOptions.IgnoreCase)]
    private static partial Regex SplitPattern();
}
