using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Agents;

namespace WorkflowFramework.Tests.TinyBDD.Agents;

[Feature("Sliding window compaction strategy")]
public class SlidingWindowCompactionStrategyTests : TinyBddTestBase
{
    public SlidingWindowCompactionStrategyTests(ITestOutputHelper output) : base(output) { }

    private static List<ConversationMessage> MakeMessages(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new ConversationMessage { Role = ConversationRole.User, Content = $"msg{i}" })
            .ToList();

    [Scenario("Summary includes omitted-count marker when messages exceed window"), Fact]
    public async Task OmitMarkerAppearsForLargeMessageList()
    {
        var strategy = new SlidingWindowCompactionStrategy(keepFirst: 2, keepLast: 2);
        var summary = await strategy.SummarizeAsync(MakeMessages(6), new CompactionOptions());

        await Given("a strategy producing a summary from 6 messages", () => summary)
            .Then("the summary contains the omission marker", s =>
            {
                s.Should().Contain("messages omitted");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Summary includes first messages from the window"), Fact]
    public async Task SummaryIncludesFirstMessages()
    {
        var strategy = new SlidingWindowCompactionStrategy(keepFirst: 2, keepLast: 2);
        var summary = await strategy.SummarizeAsync(MakeMessages(6), new CompactionOptions());

        await Given("a summary of 6 messages with first-2/last-2 window", () => summary)
            .Then("summary includes msg1 and msg2", s =>
            {
                s.Should().Contain("msg1");
                s.Should().Contain("msg2");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Summary includes last messages from the window"), Fact]
    public async Task SummaryIncludesLastMessages()
    {
        var strategy = new SlidingWindowCompactionStrategy(keepFirst: 2, keepLast: 2);
        var summary = await strategy.SummarizeAsync(MakeMessages(6), new CompactionOptions());

        await Given("a summary of 6 messages with first-2/last-2 window", () => summary)
            .Then("summary includes msg5 and msg6", s =>
            {
                s.Should().Contain("msg5");
                s.Should().Contain("msg6");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Small message lists are summarized without omission"), Fact]
    public async Task SmallListNoOmission()
    {
        var strategy = new SlidingWindowCompactionStrategy(keepFirst: 2, keepLast: 5);
        var messages = new List<ConversationMessage>
        {
            new() { Role = ConversationRole.User, Content = "a" },
            new() { Role = ConversationRole.Assistant, Content = "b" },
            new() { Role = ConversationRole.User, Content = "c" }
        };
        var summary = await strategy.SummarizeAsync(messages, new CompactionOptions());

        await Given("a summary of only 3 messages with keep-first-2/last-5 window", () => summary)
            .Then("the summary does not contain omission marker", s =>
            {
                s.Should().NotContain("messages omitted");
                s.Should().Contain("a").And.Contain("b").And.Contain("c");
                return true;
            })
            .AssertPassed();
    }
}
