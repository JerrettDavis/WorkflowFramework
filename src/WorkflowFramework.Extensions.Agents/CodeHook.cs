namespace WorkflowFramework.Extensions.Agents;

/// <summary>A hook that wraps a delegate function.</summary>
public sealed class CodeHook : IAgentHook
{
    private readonly Func<HookContext, Task<HookResult>> _handler;

    /// <summary>Initializes a new CodeHook.</summary>
    public CodeHook(Func<HookContext, Task<HookResult>> handler, string? matcher = null)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        Matcher = matcher;
    }

    /// <inheritdoc />
    public string? Matcher { get; }

    /// <inheritdoc />
    public Task<HookResult> ExecuteAsync(AgentHookEvent hookEvent, HookContext context, CancellationToken ct = default)
        => _handler(context);
}
