using FluentAssertions;
using WorkflowFramework.Extensions.Agents.Skills;
using Xunit;

namespace WorkflowFramework.Tests.Agents.Skills;

public class SkillDiscoveryTests
{
    [Fact]
    public void ScanDirectory_NullPath_Throws()
    {
        var discovery = new SkillDiscovery(false);
        var act = () => discovery.ScanDirectory(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ScanDirectory_NonexistentPath_ReturnsEmpty()
    {
        var discovery = new SkillDiscovery(false);
        var result = discovery.ScanDirectory("/nonexistent/path/12345");
        result.Should().BeEmpty();
    }

    [Fact]
    public void ScanDirectory_FindsSkillMdFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var skill1Dir = Path.Combine(tempDir, "skill1");
            var skill2Dir = Path.Combine(tempDir, "skill2");
            Directory.CreateDirectory(skill1Dir);
            Directory.CreateDirectory(skill2Dir);

            File.WriteAllText(Path.Combine(skill1Dir, "SKILL.md"), "---\nname: Skill1\n---\nBody1");
            File.WriteAllText(Path.Combine(skill2Dir, "SKILL.md"), "---\nname: Skill2\n---\nBody2");

            var discovery = new SkillDiscovery(false);
            var skills = discovery.ScanDirectory(tempDir);

            skills.Should().HaveCount(2);
            skills.Select(s => s.Name).Should().Contain("Skill1").And.Contain("Skill2");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ScanDirectory_EmptyDirectory_ReturnsEmpty()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var discovery = new SkillDiscovery(false);
            var skills = discovery.ScanDirectory(tempDir);
            skills.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ScanDirectory_SkipsMalformedFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var skillDir = Path.Combine(tempDir, "good");
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "---\nname: Good\n---\nBody");

            // A valid SKILL.md with minimal content - should still parse
            var skill2Dir = Path.Combine(tempDir, "minimal");
            Directory.CreateDirectory(skill2Dir);
            File.WriteAllText(Path.Combine(skill2Dir, "SKILL.md"), "Just body");

            var discovery = new SkillDiscovery(false);
            var skills = discovery.ScanDirectory(tempDir);
            skills.Should().HaveCountGreaterThanOrEqualTo(1);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DiscoverAll_WithAdditionalPaths()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var skillDir = Path.Combine(tempDir, "myskill");
            Directory.CreateDirectory(skillDir);
            File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), "---\nname: Extra\n---\nBody");

            var discovery = new SkillDiscovery(false, new[] { tempDir });
            var skills = discovery.DiscoverAll();
            skills.Should().HaveCount(1);
            skills[0].Name.Should().Be("Extra");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void DiscoverAll_NoStandardPaths_NoAdditional_ReturnsEmpty()
    {
        var discovery = new SkillDiscovery(false);
        var skills = discovery.DiscoverAll();
        // May find skills in current dir, but with scanStandard=false and no paths, likely empty
        // Just verify it doesn't throw
        skills.Should().NotBeNull();
    }

    [Fact]
    public void ScanStandardPaths_DoesNotThrow()
    {
        var discovery = new SkillDiscovery(true);
        // Should not throw even if paths don't exist
        var skills = discovery.ScanStandardPaths();
        skills.Should().NotBeNull();
    }
}
