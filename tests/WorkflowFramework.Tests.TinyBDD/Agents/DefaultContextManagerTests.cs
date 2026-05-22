using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Agents;

namespace WorkflowFramework.Tests.TinyBDD.Agents;

[Feature("Default context manager")]
public class DefaultContextManagerTests : TinyBddTestBase
{
    public DefaultContextManagerTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Messages added via AddMessage appear in GetMessages"), Fact]
    public async Task AddedMessagesAreRetrievable() =>
        await Given("an empty context manager", () => new DefaultContextManager())
            .When("two messages are added", mgr =>
            {
                mgr.AddMessage(new ConversationMessage { Role = ConversationRole.User, Content = "hello" });
                mgr.AddMessage(new ConversationMessage { Role = ConversationRole.Assistant, Content = "hi" });
                return mgr;
            })
            .Then("GetMessages returns both in order", mgr =>
            {
                var msgs = mgr.GetMessages();
                msgs.Should().HaveCount(2);
                msgs[0].Content.Should().Be("hello");
                msgs[1].Content.Should().Be("hi");
                return true;
            })
            .AssertPassed();

    [Scenario("AddToolCall adds a Tool-role message"), Fact]
    public async Task AddToolCallAddsMessage() =>
        await Given("an empty context manager", () => new DefaultContextManager())
            .When("a tool call result is recorded", mgr =>
            {
                mgr.AddToolCall("my-tool", "{}", "tool output");
                return mgr;
            })
            .Then("there is exactly one message with Tool role", mgr =>
            {
                var msgs = mgr.GetMessages();
                msgs.Should().HaveCount(1);
                msgs[0].Role.Should().Be(ConversationRole.Tool);
                return true;
            })
            .AssertPassed();

    [Scenario("CompactAsync with SlidingWindowCompactionStrategy preserves system messages"), Fact]
    public async Task CompactPreservesSystemMessages()
    {
        var mgr = new DefaultContextManager();
        mgr.AddMessage(new ConversationMessage { Role = ConversationRole.System, Content = "sys" });
        for (int i = 1; i <= 4; i++)
            mgr.AddMessage(new ConversationMessage { Role = ConversationRole.User, Content = $"msg{i}" });

        var opts = new CompactionOptions
        {
            PreserveSystemMessages = true,
            PreserveRecentCount = 1,
            Strategy = new SlidingWindowCompactionStrategy()
        };
        await mgr.CompactAsync(opts);

        await Given("the compacted context manager", () => mgr)
            .Then("the system message is still present", m =>
            {
                m.GetMessages().Any(x => x.Role == ConversationRole.System && x.Content == "sys").Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("CreateSnapshot captures current messages"), Fact]
    public async Task SnapshotCapturesMessages()
    {
        var mgr = new DefaultContextManager();
        mgr.AddMessage(new ConversationMessage { Role = ConversationRole.User, Content = "snap me" });
        var snapshot = mgr.CreateSnapshot();

        await Given("a snapshot from a single-message context manager", () => snapshot)
            .Then("snapshot contains the message", snap =>
            {
                snap.Messages.Should().HaveCount(1);
                snap.Messages[0].Content.Should().Be("snap me");
                return true;
            })
            .AssertPassed();
    }
}
