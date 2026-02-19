namespace WorkflowFramework.Extensions.Agents.Skills;

/// <summary>
/// Options for configuring agent skills.
/// </summary>
public sealed class SkillOptions
{
    /// <summary>Gets the list of additional paths to scan for skills.</summary>
    public IList<string> AdditionalPaths { get; set; } = new List<string>();

    /// <summary>Gets or sets whether to scan standard paths. Default is true.</summary>
    public bool ScanStandardPaths { get; set; } = true;

    /// <summary>Gets or sets whether to auto-discover skills. Default is true.</summary>
    public bool AutoDiscover { get; set; } = true;
}
