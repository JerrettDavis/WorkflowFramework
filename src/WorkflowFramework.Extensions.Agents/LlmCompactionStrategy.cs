using System.Text;
using WorkflowFramework.Extensions.AI;

namespace WorkflowFramework.Extensions.Agents;

/// <summary>
/// Compaction strategy that uses an LLM to summarize conversation history.
/// </summary>
public sealed class LlmCompactionStrategy : ICompactionStrategy
{
    private readonly IAgentProvider _provider;

    /// <summary>
    /// Initializes a new instance of <see cref="LlmCompactionStrategy"/>.
    /// </summary>
    public LlmCompactionStrategy(IAgentProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    /// <inheritdoc />
    public string Name => "LLM";

    /// <inheritdoc />
    public async Task<string> SummarizeAsync(IReadOnlyList<ConversationMessage> messages, CompactionOptions options, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Summarize the following conversation history concisely, preserving key information:");
        if (!string.IsNullOrEmpty(options.FocusInstructions))
        {
            sb.AppendLine($"Focus on: {options.FocusInstructions}");
        }
        sb.AppendLine();

        foreach (var msg in messages)
        {
            sb.AppendLine($"[{msg.Role}]: {msg.Content}");
        }

        var request = new LlmRequest { Prompt = sb.ToString() };
        var response = await _provider.CompleteAsync(request, ct).ConfigureAwait(false);
        return response.Content;
    }
}
