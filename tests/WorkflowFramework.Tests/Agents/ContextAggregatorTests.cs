using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Agents;
using Xunit;

namespace WorkflowFramework.Tests.Agents;

public class ContextAggregatorTests
{
    [Fact]
    public async Task GetAllContextAsync_CombinesMultipleSources()
    {
        var s1 = Substitute.For<IContextSource>();
        s1.GetContextAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ContextDocument> { new() { Name = "doc1", Content = "c1" } });
        var s2 = Substitute.For<IContextSource>();
        s2.GetContextAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ContextDocument> { new() { Name = "doc2", Content = "c2" } });

        var agg = new ContextAggregator(new[] { s1, s2 });
        var docs = await agg.GetAllContextAsync();

        docs.Should().HaveCount(2);
        docs.Select(d => d.Name).Should().Contain("doc1").And.Contain("doc2");
    }

    [Fact]
    public async Task GetAllContextAsync_EmptySources_ReturnsEmpty()
    {
        var agg = new ContextAggregator();
        var docs = await agg.GetAllContextAsync();
        docs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllContextAsync_SingleSource()
    {
        var s1 = Substitute.For<IContextSource>();
        s1.GetContextAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ContextDocument> { new() { Name = "only", Content = "content" } });

        var agg = new ContextAggregator();
        agg.Add(s1);
        var docs = await agg.GetAllContextAsync();

        docs.Should().HaveCount(1);
        docs[0].Name.Should().Be("only");
    }

    [Fact]
    public async Task BuildContextPromptAsync_ReturnsFormattedString()
    {
        var s1 = Substitute.For<IContextSource>();
        s1.GetContextAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ContextDocument> { new() { Name = "TestDoc", Content = "test content", Source = "src" } });

        var agg = new ContextAggregator(new[] { s1 });
        var prompt = await agg.BuildContextPromptAsync();

        prompt.Should().Contain("## Context");
        prompt.Should().Contain("### TestDoc");
        prompt.Should().Contain("test content");
    }

    [Fact]
    public async Task BuildContextPromptAsync_EmptySources_ReturnsEmpty()
    {
        var agg = new ContextAggregator();
        var prompt = await agg.BuildContextPromptAsync();
        prompt.Should().BeEmpty();
    }

    [Fact]
    public void Add_NullSource_Throws()
    {
        var agg = new ContextAggregator();
        var act = () => agg.Add(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Sources_ReturnsAddedSources()
    {
        var s1 = Substitute.For<IContextSource>();
        var agg = new ContextAggregator();
        agg.Add(s1);
        agg.Sources.Should().HaveCount(1);
    }
}
