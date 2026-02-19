namespace WorkflowFramework.Extensions.Agents.Skills;

/// <summary>
/// Scans directories for SKILL.md files.
/// </summary>
public sealed class SkillDiscovery
{
    private readonly List<string> _additionalPaths = new();
    private readonly bool _scanStandardPaths;

    /// <summary>
    /// Initializes a new instance of <see cref="SkillDiscovery"/>.
    /// </summary>
    public SkillDiscovery(bool scanStandardPaths = true, IEnumerable<string>? additionalPaths = null)
    {
        _scanStandardPaths = scanStandardPaths;
        if (additionalPaths != null)
            _additionalPaths.AddRange(additionalPaths);
    }

    /// <summary>
    /// Scans a directory for */SKILL.md files.
    /// </summary>
    public IReadOnlyList<SkillDefinition> ScanDirectory(string basePath)
    {
        if (basePath == null) throw new ArgumentNullException(nameof(basePath));
        if (!Directory.Exists(basePath)) return Array.Empty<SkillDefinition>();

        var skills = new List<SkillDefinition>();
        try
        {
            foreach (var file in Directory.GetFiles(basePath, "SKILL.md", SearchOption.AllDirectories))
            {
                try
                {
                    skills.Add(SkillLoader.ParseFile(file));
                }
                catch
                {
                    // Skip malformed files
                }
            }
        }
        catch
        {
            // Skip inaccessible directories
        }
        return skills;
    }

    /// <summary>
    /// Scans standard skill paths (~/.agents/skills/, ~/.claude/skills/, .agents/skills/, .claude/skills/).
    /// </summary>
    public IReadOnlyList<SkillDefinition> ScanStandardPaths()
    {
        var skills = new List<SkillDefinition>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var standardPaths = new[]
        {
            Path.Combine(home, ".agents", "skills"),
            Path.Combine(home, ".claude", "skills"),
            Path.Combine(".", ".agents", "skills"),
            Path.Combine(".", ".claude", "skills")
        };

        foreach (var path in standardPaths)
        {
            skills.AddRange(ScanDirectory(path));
        }

        return skills;
    }

    /// <summary>
    /// Discovers all skills from standard paths and additional paths.
    /// </summary>
    public IReadOnlyList<SkillDefinition> DiscoverAll()
    {
        var skills = new List<SkillDefinition>();
        if (_scanStandardPaths)
        {
            skills.AddRange(ScanStandardPaths());
        }
        foreach (var path in _additionalPaths)
        {
            skills.AddRange(ScanDirectory(path));
        }
        return skills;
    }
}
