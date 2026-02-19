namespace WorkflowFramework.Extensions.Agents;

/// <summary>Hook for intercepting agent execution lifecycle events.</summary>
public interface IAgentHook
{
    /// <summary>Gets a regex pattern to match against event context. Null matches all.</summary>
    string? Matcher { get; }
    /// <summary>Executes the hook for the given event.</summary>
    Task<HookResult> ExecuteAsync(AgentHookEvent hookEvent, HookContext context, CancellationToken ct = default);
}
