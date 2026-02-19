namespace WorkflowFramework.Extensions.Agents.Skills;

/// <summary>
/// Parses SKILL.md files into <see cref="SkillDefinition"/> instances.
/// </summary>
public static class SkillLoader
{
    /// <summary>
    /// Parses a SKILL.md file from disk.
    /// </summary>
    public static SkillDefinition ParseFile(string path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        var content = File.ReadAllText(path);
        var skill = Parse(content);
        skill.SourcePath = path;
        return skill;
    }

    /// <summary>
    /// Parses SKILL.md content (frontmatter + body).
    /// </summary>
    public static SkillDefinition Parse(string content)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));

        var frontmatter = new SkillFrontmatter();
        string body;

        // Split frontmatter from body
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith("---", StringComparison.Ordinal))
        {
            var endIndex = trimmed.IndexOf("---", 3, StringComparison.Ordinal);
            if (endIndex > 0)
            {
                var yamlSection = trimmed.Substring(3, endIndex - 3).Trim();
                body = trimmed.Substring(endIndex + 3).TrimStart('\r', '\n');
                frontmatter = ParseYaml(yamlSection);
            }
            else
            {
                // No closing ---, treat all as body
                body = content;
            }
        }
        else
        {
            body = content;
        }

        return new SkillDefinition
        {
            Name = frontmatter.Name ?? string.Empty,
            Description = frontmatter.Description ?? string.Empty,
            License = frontmatter.License,
            Compatibility = frontmatter.Compatibility,
            Metadata = frontmatter.Metadata,
            AllowedTools = frontmatter.AllowedTools,
            Body = body
        };
    }

    internal static SkillFrontmatter ParseYaml(string yaml)
    {
        var fm = new SkillFrontmatter();
        var lines = yaml.Split('\n');
        string? currentKey = null;
        var inMetadata = false;
        var inAllowedTools = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            // Check for list item under a key
            if ((inAllowedTools || inMetadata) && line.StartsWith("  - ", StringComparison.Ordinal))
            {
                var value = line.Substring(4).Trim();
                if (inAllowedTools)
                {
                    fm.AllowedTools.Add(value);
                }
                continue;
            }

            // Check for metadata sub-key
            if (inMetadata && line.StartsWith("  ", StringComparison.Ordinal) && line.Contains(":"))
            {
                var subLine = line.TrimStart();
                var colonIdx = subLine.IndexOf(':');
                if (colonIdx > 0)
                {
                    var subKey = subLine.Substring(0, colonIdx).Trim();
                    var subVal = subLine.Substring(colonIdx + 1).Trim();
                    fm.Metadata[subKey] = subVal;
                }
                continue;
            }

            // Top-level key
            if (!line.StartsWith(" ", StringComparison.Ordinal) && line.Contains(":"))
            {
                inMetadata = false;
                inAllowedTools = false;

                var colonIdx = line.IndexOf(':');
                var key = line.Substring(0, colonIdx).Trim();
                var val = line.Substring(colonIdx + 1).Trim();
                currentKey = key;

                switch (key.ToLowerInvariant())
                {
                    case "name":
                        fm.Name = val;
                        break;
                    case "description":
                        fm.Description = val;
                        break;
                    case "license":
                        fm.License = val;
                        break;
                    case "compatibility":
                        fm.Compatibility = val;
                        break;
                    case "metadata":
                        inMetadata = true;
                        break;
                    case "allowed-tools":
                        inAllowedTools = true;
                        break;
                }
            }
        }

        return fm;
    }
}
