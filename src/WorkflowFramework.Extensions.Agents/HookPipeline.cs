using System.Text.RegularExpressions;

namespace WorkflowFramework.Extensions.Agents;

/// <summary>Ordered list of hooks. Fires matching hooks per event. Deny stops execution.</summary>
public sealed class HookPipeline
{
    private readonly List<IAgentHook> _hooks = new();

    /// <summary>Initializes empty pipeline.</summary>
    public HookPipeline() { }

    /// <summary>Initializes with hooks.</summary>
    public HookPipeline(IEnumerable<IAgentHook> hooks)
    {
        if (hooks == null) throw new ArgumentNullException(nameof(hooks));
        _hooks.AddRange(hooks);
    }

    /// <summary>Adds a hook.</summary>
    public void Add(IAgentHook hook)
    {
        if (hook == null) throw new ArgumentNullException(nameof(hook));
        _hooks.Add(hook);
    }

    /// <summary>Gets the hooks.</summary>
    public IReadOnlyList<IAgentHook> Hooks => _hooks.AsReadOnly();

    /// <summary>Fires matching hooks. Deny stops. Last modify wins.</summary>
    public async Task<HookResult> FireAsync(AgentHookEvent hookEvent, HookContext context, CancellationToken ct = default)
    {
        var aggregate = HookResult.AllowResult();
        var matchTarget = hookEvent.ToString();
        if (!string.IsNullOrEmpty(context.StepName)) matchTarget += ":" + context.StepName;
        if (!string.IsNullOrEmpty(context.ToolName)) matchTarget += ":" + context.ToolName;

        foreach (var hook in _hooks)
        {
            if (hook.Matcher != null)
            {
                try { if (!Regex.IsMatch(matchTarget, hook.Matcher)) continue; }
                catch { continue; }
            }

            var result = await hook.ExecuteAsync(hookEvent, context, ct).ConfigureAwait(false);
            if (result.Decision == HookDecision.Deny) return result;
            if (result.Decision == HookDecision.Modify)
            {
                aggregate = result;
                if (result.ModifiedArgs != null) context.ToolArgs = result.ModifiedArgs;
            }
            else
            {
                aggregate = result;
            }
        }
        return aggregate;
    }
}
