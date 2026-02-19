using FluentAssertions;
using WorkflowFramework.Extensions.Agents.Skills;
using Xunit;

namespace WorkflowFramework.Tests.Agents.Skills;

public class SkillToolProviderTests
{
    [Fact]
    public void Constructor_NullSkills_Throws()
    {
        var act = () => new SkillToolProvider(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ListToolsAsync_ReturnsToolPerSkill()
    {
        var skills = new List<SkillDefinition>
        {
            new() { Name = "skill1", Description = "desc1", SourcePath = "/path/skill1" },
            new() { Name = "skill2", Description = "desc2" }
        };
        var provider = new SkillToolProvider(skills);

        var tools = await provider.ListToolsAsync();

        tools.Should().HaveCount(2);
        tools[0].Name.Should().Be("skill1");
        tools[0].Description.Should().Be("desc1");
        tools[0].Metadata["source"].Should().Be("skill");
        tools[0].Metadata["sourcePath"].Should().Be("/path/skill1");
        tools[1].Name.Should().Be("skill2");
    }

    [Fact]
    public async Task ListToolsAsync_EmptySkills_ReturnsEmpty()
    {
        var provider = new SkillToolProvider(new List<SkillDefinition>());
        var tools = await provider.ListToolsAsync();
        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task InvokeToolAsync_ReturnsSkillBody()
    {
        var skills = new List<SkillDefinition>
        {
            new() { Name = "myskill", Body = "Do this: step 1, step 2" }
        };
        var provider = new SkillToolProvider(skills);

        var result = await provider.InvokeToolAsync("myskill", "{}");

        result.Content.Should().Be("Do this: step 1, step 2");
        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeToolAsync_SkillNotFound_ReturnsError()
    {
        var provider = new SkillToolProvider(new List<SkillDefinition>());
        var result = await provider.InvokeToolAsync("missing", "{}");
        result.IsError.Should().BeTrue();
        result.Content.Should().Contain("missing");
    }

    [Fact]
    public async Task InvokeToolAsync_MultipleSkills_FindsCorrectOne()
    {
        var skills = new List<SkillDefinition>
        {
            new() { Name = "skill1", Body = "body1" },
            new() { Name = "skill2", Body = "body2" }
        };
        var provider = new SkillToolProvider(skills);

        var result = await provider.InvokeToolAsync("skill2", "{}");
        result.Content.Should().Be("body2");
    }

    [Fact]
    public async Task ListToolsAsync_NoSourcePath_OmitsFromMetadata()
    {
        var skills = new List<SkillDefinition>
        {
            new() { Name = "skill1", Description = "desc" }
        };
        var provider = new SkillToolProvider(skills);
        var tools = await provider.ListToolsAsync();
        tools[0].Metadata.Should().NotContainKey("sourcePath");
    }
}

public class SkillContextSourceTests
{
    [Fact]
    public void Constructor_NullSkills_Throws()
    {
        var act = () => new SkillContextSource(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Name_IsSkills()
    {
        var source = new SkillContextSource(new List<SkillDefinition>());
        source.Name.Should().Be("skills");
    }

    [Fact]
    public async Task GetContextAsync_ReturnsDocPerSkill()
    {
        var skills = new List<SkillDefinition>
        {
            new() { Name = "s1", Description = "d1", Body = "body1", SourcePath = "/p" },
            new() { Name = "s2", Description = "d2", Body = "body2" }
        };
        var source = new SkillContextSource(skills);

        var docs = await source.GetContextAsync();

        docs.Should().HaveCount(2);
        docs[0].Name.Should().Be("s1");
        docs[0].Content.Should().Be("body1");
        docs[0].Source.Should().Be("/p");
        docs[0].Metadata["description"].Should().Be("d1");
        docs[1].Source.Should().Be("skill");
    }

    [Fact]
    public async Task GetContextAsync_EmptySkills_ReturnsEmpty()
    {
        var source = new SkillContextSource(new List<SkillDefinition>());
        var docs = await source.GetContextAsync();
        docs.Should().BeEmpty();
    }
}
