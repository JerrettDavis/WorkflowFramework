using System.Text;

namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Compaction strategy that keeps first N + last M messages, drops the middle.
/// No LLM needed.
/// </summary>
public sealed class SlidingWindowCompactionStrategy : ICompactionStrategy
{
    private readonly int _keepFirst;
    private readonly int _keepLast;

    /// <summary>
    /// Initializes a new instance of <see cref="SlidingWindowCompactionStrategy"/>.
    /// </summary>
    /// <param name="keepFirst">Number of first messages to keep. Default 2.</param>
    /// <param name="keepLast">Number of last messages to keep. Default 5.</param>
    public SlidingWindowCompactionStrategy(int keepFirst = 2, int keepLast = 5)
    {
        _keepFirst = Math.Max(0, keepFirst);
        _keepLast = Math.Max(0, keepLast);
    }

    /// <inheritdoc />
    public string Name => "SlidingWindow";

    /// <inheritdoc />
    public Task<string> SummarizeAsync(IReadOnlyList<ConversationMessage> messages, CompactionOptions options, CancellationToken ct = default)
    {
        if (messages.Count <= _keepFirst + _keepLast)
        {
            // Nothing to drop, summarize all
            var sb = new StringBuilder();
            sb.AppendLine("[Conversation summary]");
            foreach (var msg in messages)
            {
                sb.AppendLine($"[{msg.Role}]: {msg.Content}");
            }
            return Task.FromResult(sb.ToString().TrimEnd());
        }

        var first = messages.Take(_keepFirst);
        var last = messages.Skip(messages.Count - _keepLast).Take(_keepLast);
        var droppedCount = messages.Count - _keepFirst - _keepLast;

        var result = new StringBuilder();
        result.AppendLine("[Conversation summary]");
        foreach (var msg in first)
        {
            result.AppendLine($"[{msg.Role}]: {msg.Content}");
        }
        result.AppendLine($"[... {droppedCount} messages omitted ...]");
        foreach (var msg in last)
        {
            result.AppendLine($"[{msg.Role}]: {msg.Content}");
        }

        return Task.FromResult(result.ToString().TrimEnd());
    }
}
