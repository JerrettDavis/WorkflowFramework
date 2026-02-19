namespace WorkflowFramework.Extensions.Agents.Skills;

/// <summary>
/// Exposes skill instructions as context documents for agent prompts.
/// </summary>
public sealed class SkillContextSource : IContextSource
{
    private readonly IReadOnlyList<SkillDefinition> _skills;

    /// <summary>
    /// Initializes a new instance of <see cref="SkillContextSource"/>.
    /// </summary>
    public SkillContextSource(IReadOnlyList<SkillDefinition> skills)
    {
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
    }

    /// <inheritdoc />
    public string Name => "skills";

    /// <inheritdoc />
    public Task<IReadOnlyList<ContextDocument>> GetContextAsync(CancellationToken ct = default)
    {
        var docs = new List<ContextDocument>();
        foreach (var skill in _skills)
        {
            docs.Add(new ContextDocument
            {
                Name = skill.Name,
                Content = skill.Body,
                Source = skill.SourcePath ?? "skill",
                Metadata = new Dictionary<string, string>
                {
                    ["description"] = skill.Description
                }
            });
        }
        return Task.FromResult<IReadOnlyList<ContextDocument>>(docs);
    }
}
