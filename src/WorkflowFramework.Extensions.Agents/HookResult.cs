namespace WorkflowFramework.Extensions.Agents;

/// <summary>Result returned by an agent hook.</summary>
public sealed class HookResult
{
    /// <summary>Gets or sets the decision.</summary>
    public HookDecision Decision { get; set; } = HookDecision.Allow;
    /// <summary>Gets or sets the reason.</summary>
    public string? Reason { get; set; }
    /// <summary>Gets or sets modified arguments.</summary>
    public string? ModifiedArgs { get; set; }
    /// <summary>Gets or sets an output message.</summary>
    public string? OutputMessage { get; set; }

    /// <summary>Creates an Allow result.</summary>
    public static HookResult AllowResult(string? reason = null) => new() { Decision = HookDecision.Allow, Reason = reason };
    /// <summary>Creates a Deny result.</summary>
    public static HookResult DenyResult(string? reason = null) => new() { Decision = HookDecision.Deny, Reason = reason };
    /// <summary>Creates a Modify result.</summary>
    public static HookResult ModifyResult(string modifiedArgs, string? reason = null) =>
        new() { Decision = HookDecision.Modify, ModifiedArgs = modifiedArgs, Reason = reason };
}
