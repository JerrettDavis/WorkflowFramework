namespace WorkflowFramework.Extensions.Agents;

/// <summary>Decision returned by an agent hook.</summary>
public enum HookDecision
{
    /// <summary>Allow the operation.</summary>
    Allow,
    /// <summary>Deny the operation.</summary>
    Deny,
    /// <summary>Modify the operation.</summary>
    Modify
}
