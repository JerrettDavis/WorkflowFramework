namespace WorkflowFramework.Extensions.Agents.Skills;

/// <summary>
/// Exposes discovered skills as invocable tools.
/// </summary>
public sealed class SkillToolProvider : IToolProvider
{
    private readonly IReadOnlyList<SkillDefinition> _skills;

    /// <summary>
    /// Initializes a new instance of <see cref="SkillToolProvider"/>.
    /// </summary>
    public SkillToolProvider(IReadOnlyList<SkillDefinition> skills)
    {
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken ct = default)
    {
        var tools = new List<ToolDefinition>();
        foreach (var skill in _skills)
        {
            var metadata = new Dictionary<string, string> { ["source"] = "skill" };
            if (skill.SourcePath != null)
                metadata["sourcePath"] = skill.SourcePath;

            tools.Add(new ToolDefinition
            {
                Name = skill.Name,
                Description = skill.Description,
                Metadata = metadata
            });
        }
        return Task.FromResult<IReadOnlyList<ToolDefinition>>(tools);
    }

    /// <inheritdoc />
    public Task<ToolResult> InvokeToolAsync(string toolName, string argumentsJson, CancellationToken ct = default)
    {
        foreach (var skill in _skills)
        {
            if (skill.Name == toolName)
            {
                return Task.FromResult(new ToolResult
                {
                    Content = skill.Body,
                    IsError = false
                });
            }
        }

        return Task.FromResult(new ToolResult
        {
            Content = $"Skill '{toolName}' not found.",
            IsError = true
        });
    }
}
