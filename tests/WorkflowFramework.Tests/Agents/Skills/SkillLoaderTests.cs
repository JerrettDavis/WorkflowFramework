using FluentAssertions;
using WorkflowFramework.Extensions.Agents.Skills;
using Xunit;

namespace WorkflowFramework.Tests.Agents.Skills;

public class SkillLoaderTests
{
    [Fact]
    public void Parse_NullContent_Throws()
    {
        var act = () => SkillLoader.Parse(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Parse_FullFrontmatter()
    {
        var content = """
            ---
            name: MySkill
            description: A test skill
            license: MIT
            compatibility: claude, gpt-4
            metadata:
              author: test
              version: 1.0
            allowed-tools:
              - tool1
              - tool2
            ---
            # Skill Body

            Do the thing.
            """;

        var skill = SkillLoader.Parse(content);

        skill.Name.Should().Be("MySkill");
        skill.Description.Should().Be("A test skill");
        skill.License.Should().Be("MIT");
        skill.Compatibility.Should().Be("claude, gpt-4");
        skill.Metadata.Should().ContainKey("author").WhoseValue.Should().Be("test");
        skill.Metadata.Should().ContainKey("version").WhoseValue.Should().Be("1.0");
        skill.AllowedTools.Should().Contain("tool1").And.Contain("tool2");
        skill.Body.Should().Contain("# Skill Body");
        skill.Body.Should().Contain("Do the thing.");
    }

    [Fact]
    public void Parse_MinimalFrontmatter()
    {
        var content = """
            ---
            name: Simple
            ---
            Body content
            """;

        var skill = SkillLoader.Parse(content);
        skill.Name.Should().Be("Simple");
        skill.Description.Should().BeEmpty();
        skill.License.Should().BeNull();
        skill.Body.Should().Contain("Body content");
    }

    [Fact]
    public void Parse_NoFrontmatter_AllBody()
    {
        var content = "# Just a markdown file\n\nContent here.";
        var skill = SkillLoader.Parse(content);
        skill.Name.Should().BeEmpty();
        skill.Body.Should().Contain("Just a markdown file");
    }

    [Fact]
    public void Parse_MalformedFrontmatter_NoClosing()
    {
        var content = "---\nname: Broken\nStill going";
        var skill = SkillLoader.Parse(content);
        // No closing ---, treat all as body
        skill.Name.Should().BeEmpty();
        skill.Body.Should().Contain("---");
    }

    [Fact]
    public void Parse_EmptyContent()
    {
        var skill = SkillLoader.Parse("");
        skill.Name.Should().BeEmpty();
        skill.Body.Should().BeEmpty();
    }

    [Fact]
    public void Parse_OnlyFrontmatter_EmptyBody()
    {
        var content = "---\nname: NoBody\n---\n";
        var skill = SkillLoader.Parse(content);
        skill.Name.Should().Be("NoBody");
        skill.Body.Should().BeEmpty();
    }

    [Fact]
    public void Parse_FrontmatterWithDescription()
    {
        var content = "---\nname: Test\ndescription: My description\n---\nBody";
        var skill = SkillLoader.Parse(content);
        skill.Description.Should().Be("My description");
    }

    [Fact]
    public void ParseFile_NullPath_Throws()
    {
        var act = () => SkillLoader.ParseFile(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ParseFile_ValidFile_SetsSourcePath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var filePath = Path.Combine(tempDir, "SKILL.md");
            File.WriteAllText(filePath, "---\nname: FileSkill\n---\nBody");

            var skill = SkillLoader.ParseFile(filePath);
            skill.Name.Should().Be("FileSkill");
            skill.SourcePath.Should().Be(filePath);
            skill.Body.Should().Contain("Body");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ParseFile_NonexistentFile_Throws()
    {
        var act = () => SkillLoader.ParseFile("/nonexistent/path/SKILL.md");
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Parse_MetadataOnly()
    {
        var content = "---\nmetadata:\n  key1: val1\n  key2: val2\n---\nBody";
        var skill = SkillLoader.Parse(content);
        skill.Metadata.Should().ContainKey("key1").WhoseValue.Should().Be("val1");
        skill.Metadata.Should().ContainKey("key2").WhoseValue.Should().Be("val2");
    }

    [Fact]
    public void Parse_AllowedToolsOnly()
    {
        var content = "---\nallowed-tools:\n  - toolA\n  - toolB\n---\nBody";
        var skill = SkillLoader.Parse(content);
        skill.AllowedTools.Should().HaveCount(2);
        skill.AllowedTools.Should().Contain("toolA");
        skill.AllowedTools.Should().Contain("toolB");
    }

    [Fact]
    public void Parse_WhitespaceBeforeFrontmatter()
    {
        var content = "  \n---\nname: Indented\n---\nBody";
        var skill = SkillLoader.Parse(content);
        skill.Name.Should().Be("Indented");
    }
}
