namespace WorkflowFramework.Extensions.Agents.Skills;

/// <summary>
/// Represents a parsed agent skill.
/// </summary>
public sealed class SkillDefinition
{
    /// <summary>Gets or sets the skill name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the skill description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the license.</summary>
    public string? License { get; set; }

    /// <summary>Gets or sets compatibility info.</summary>
    public string? Compatibility { get; set; }

    /// <summary>Gets or sets arbitrary metadata.</summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

    /// <summary>Gets or sets the allowed tools list.</summary>
    public IList<string> AllowedTools { get; set; } = new List<string>();

    /// <summary>Gets or sets the markdown body content.</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>Gets or sets the source file path.</summary>
    public string? SourcePath { get; set; }
}
