namespace WorkflowFramework.Extensions.Agents.Skills;

/// <summary>
/// Parsed YAML frontmatter fields from a SKILL.md file.
/// </summary>
public sealed class SkillFrontmatter
{
    /// <summary>Gets or sets the skill name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the skill description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the license.</summary>
    public string? License { get; set; }

    /// <summary>Gets or sets compatibility info.</summary>
    public string? Compatibility { get; set; }

    /// <summary>Gets or sets arbitrary metadata.</summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

    /// <summary>Gets or sets the allowed tools list.</summary>
    public IList<string> AllowedTools { get; set; } = new List<string>();
}
