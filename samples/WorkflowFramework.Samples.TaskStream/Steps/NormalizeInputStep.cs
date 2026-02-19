using System.Text.RegularExpressions;
using WorkflowFramework.Samples.TaskStream.Models;

namespace WorkflowFramework.Samples.TaskStream.Steps;

/// <summary>
/// Cleans and normalizes raw message content.
/// </summary>
public sealed partial class NormalizeInputStep : IStep
{
    /// <inheritdoc />
    public string Name => "NormalizeInput";

    /// <inheritdoc />
    public Task ExecuteAsync(IWorkflowContext context)
    {
        var messages = (List<SourceMessage>)context.Properties["sourceMessages"]!;
        var normalized = new List<string>();

        foreach (var msg in messages)
        {
            var text = msg.RawContent.Trim();
            // Collapse whitespace
            text = WhitespacePattern().Replace(text, " ");
            // Remove common email prefixes
            text = EmailPrefixPattern().Replace(text, "");
            normalized.Add(text);
        }

        context.Properties["normalizedMessages"] = normalized;
        Console.WriteLine($"  🧹 Normalized {normalized.Count} messages");
        return Task.CompletedTask;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    [GeneratedRegex(@"^(Hi team,?\s*|Reminder:\s*|FYI:\s*|Please\s+)", RegexOptions.IgnoreCase)]
    private static partial Regex EmailPrefixPattern();
}
