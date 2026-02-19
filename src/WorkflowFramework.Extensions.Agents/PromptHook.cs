using WorkflowFramework.Extensions.AI;

namespace WorkflowFramework.Extensions.Agents;

/// <summary>Uses an IAgentProvider to evaluate whether to allow/deny.</summary>
public sealed class PromptHook : IAgentHook
{
    private readonly IAgentProvider _provider;
    private readonly string _promptTemplate;

    /// <summary>Initializes a new PromptHook.</summary>
    public PromptHook(IAgentProvider provider, string promptTemplate, string? matcher = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _promptTemplate = promptTemplate ?? throw new ArgumentNullException(nameof(promptTemplate));
        Matcher = matcher;
    }

    /// <inheritdoc />
    public string? Matcher { get; }

    /// <inheritdoc />
    public async Task<HookResult> ExecuteAsync(AgentHookEvent hookEvent, HookContext context, CancellationToken ct = default)
    {
        var prompt = _promptTemplate
            .Replace("{event}", hookEvent.ToString())
            .Replace("{toolName}", context.ToolName ?? "")
            .Replace("{toolArgs}", context.ToolArgs ?? "");

        var request = new LlmRequest { Prompt = prompt };
        var response = await _provider.CompleteAsync(request, ct).ConfigureAwait(false);
        var content = response.Content.Trim().ToLowerInvariant();

        if (content.Contains("deny") || content.Contains("block") || content.Contains("reject"))
            return HookResult.DenyResult(response.Content);

        return HookResult.AllowResult(response.Content);
    }
}
