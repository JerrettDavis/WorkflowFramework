using FluentAssertions;
using WorkflowFramework.Extensions.Agents;
using Xunit;

namespace WorkflowFramework.Tests.Agents;

public class SlidingWindowCompactionStrategyTests
{
    [Fact]
    public void Name_IsSlidingWindow()
    {
        var strategy = new SlidingWindowCompactionStrategy();
        strategy.Name.Should().Be("SlidingWindow");
    }

    [Fact]
    public async Task SummarizeAsync_KeepsFirstAndLast_DropsMiddle()
    {
        var strategy = new SlidingWindowCompactionStrategy(keepFirst: 1, keepLast: 1);
        var messages = new List<ConversationMessage>
        {
            new() { Role = ConversationRole.User, Content = "first" },
            new() { Role = ConversationRole.User, Content = "middle1" },
            new() { Role = ConversationRole.User, Content = "middle2" },
            new() { Role = ConversationRole.User, Content = "last" }
        };

        var result = await strategy.SummarizeAsync(messages, new CompactionOptions());

        result.Should().Contain("first");
        result.Should().Contain("last");
        result.Should().NotContain("middle1");
        result.Should().NotContain("middle2");
        result.Should().Contain("2 messages omitted");
    }

    [Fact]
    public async Task SummarizeAsync_FewerMessagesThanWindow_KeepsAll()
    {
        var strategy = new SlidingWindowCompactionStrategy(keepFirst: 5, keepLast: 5);
        var messages = new List<ConversationMessage>
        {
            new() { Role = ConversationRole.User, Content = "only one" }
        };

        var result = await strategy.SummarizeAsync(messages, new CompactionOptions());

        result.Should().Contain("only one");
        result.Should().NotContain("omitted");
    }

    [Fact]
    public async Task SummarizeAsync_ExactWindowSize_KeepsAll()
    {
        var strategy = new SlidingWindowCompactionStrategy(keepFirst: 2, keepLast: 2);
        var messages = new List<ConversationMessage>
        {
            new() { Role = ConversationRole.User, Content = "a" },
            new() { Role = ConversationRole.User, Content = "b" },
            new() { Role = ConversationRole.User, Content = "c" },
            new() { Role = ConversationRole.User, Content = "d" }
        };

        var result = await strategy.SummarizeAsync(messages, new CompactionOptions());

        result.Should().Contain("a");
        result.Should().Contain("d");
        result.Should().NotContain("omitted");
    }
}
