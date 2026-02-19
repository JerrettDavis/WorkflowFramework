using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Agents;
using Xunit;

namespace WorkflowFramework.Tests.Agents;

public class DefaultContextManagerTests
{
    [Fact]
    public void AddMessage_NullMessage_Throws()
    {
        var mgr = new DefaultContextManager();
        var act = () => mgr.AddMessage(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddMessage_AddsToMessages()
    {
        var mgr = new DefaultContextManager();
        var msg = new ConversationMessage { Role = ConversationRole.User, Content = "hello" };
        mgr.AddMessage(msg);
        mgr.GetMessages().Should().HaveCount(1);
        mgr.GetMessages()[0].Content.Should().Be("hello");
    }

    [Fact]
    public void AddToolCall_CreatesToolRoleMessage()
    {
        var mgr = new DefaultContextManager();
        mgr.AddToolCall("myTool", "{\"x\":1}", "result123");
        var msgs = mgr.GetMessages();
        msgs.Should().HaveCount(1);
        msgs[0].Role.Should().Be(ConversationRole.Tool);
        msgs[0].Content.Should().Contain("myTool");
        msgs[0].Content.Should().Contain("result123");
        msgs[0].Metadata["toolName"].Should().Be("myTool");
        msgs[0].Metadata["args"].Should().Be("{\"x\":1}");
    }

    [Fact]
    public void GetMessages_ReturnsReadOnlyList()
    {
        var mgr = new DefaultContextManager();
        mgr.AddMessage(new ConversationMessage { Content = "a" });
        mgr.AddMessage(new ConversationMessage { Content = "b" });
        mgr.GetMessages().Should().HaveCount(2);
    }

    [Fact]
    public void EstimateTokenCount_UsesCharsDiv4()
    {
        var mgr = new DefaultContextManager();
        // "hello world" = 11 chars => (11+3)/4 = 3
        mgr.AddMessage(new ConversationMessage { Content = "hello world" });
        mgr.EstimateTokenCount().Should().Be(3);
    }

    [Fact]
    public void EstimateTokenCount_EmptyMessages_ReturnsZero()
    {
        var mgr = new DefaultContextManager();
        mgr.EstimateTokenCount().Should().Be(0);
    }

    [Fact]
    public async Task CompactAsync_PreservesSystemMessagesAndRecent()
    {
        var mgr = new DefaultContextManager();
        mgr.AddMessage(new ConversationMessage { Role = ConversationRole.System, Content = "system prompt" });
        for (int i = 0; i < 10; i++)
            mgr.AddMessage(new ConversationMessage { Role = ConversationRole.User, Content = $"msg{i}" });

        var strategy = Substitute.For<ICompactionStrategy>();
        strategy.SummarizeAsync(Arg.Any<IReadOnlyList<ConversationMessage>>(), Arg.Any<CompactionOptions>(), Arg.Any<CancellationToken>())
            .Returns("summary of old messages");

        var options = new CompactionOptions
        {
            PreserveSystemMessages = true,
            PreserveRecentCount = 3,
            Strategy = strategy
        };

        var result = await mgr.CompactAsync(options);

        result.OriginalMessageCount.Should().Be(11);
        var msgs = mgr.GetMessages();
        // system + summary + 3 recent
        msgs[0].Role.Should().Be(ConversationRole.System);
        msgs[0].Content.Should().Be("system prompt");
        msgs[1].IsCompacted.Should().BeTrue();
        msgs[1].Content.Should().Be("summary of old messages");
        msgs.Should().HaveCount(5);
    }

    [Fact]
    public async Task CompactAsync_NullOptions_Throws()
    {
        var mgr = new DefaultContextManager();
        var act = async () => await mgr.CompactAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void CreateSnapshot_RestoreSnapshot_RoundTrips()
    {
        var mgr = new DefaultContextManager();
        mgr.AddMessage(new ConversationMessage { Role = ConversationRole.User, Content = "hello" });
        mgr.AddMessage(new ConversationMessage { Role = ConversationRole.Assistant, Content = "world" });

        var snapshot = mgr.CreateSnapshot();
        snapshot.Messages.Should().HaveCount(2);

        mgr.Clear();
        mgr.GetMessages().Should().BeEmpty();

        mgr.RestoreSnapshot(snapshot);
        mgr.GetMessages().Should().HaveCount(2);
        mgr.GetMessages()[0].Content.Should().Be("hello");
    }

    [Fact]
    public void RestoreSnapshot_NullSnapshot_Throws()
    {
        var mgr = new DefaultContextManager();
        var act = () => mgr.RestoreSnapshot(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Clear_RemovesAllMessages()
    {
        var mgr = new DefaultContextManager();
        mgr.AddMessage(new ConversationMessage { Content = "x" });
        mgr.Clear();
        mgr.GetMessages().Should().BeEmpty();
    }
}
