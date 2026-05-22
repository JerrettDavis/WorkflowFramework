using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Tests.TinyBDD.Support;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.AI;

namespace WorkflowFramework.Tests.TinyBDD.Agents;

[Feature("LLM compaction strategy")]
public class LlmCompactionStrategyTests : TinyBddTestBase
{
    public LlmCompactionStrategyTests(ITestOutputHelper output) : base(output) { }

    private static List<ConversationMessage> TwoMessages() => new()
    {
        new() { Role = ConversationRole.User, Content = "hello" },
        new() { Role = ConversationRole.Assistant, Content = "world" }
    };

    [Scenario("SummarizeAsync delegates to the provider and returns its response"), Fact]
    public async Task DelegatesToProvider()
    {
        var provider = Substitute.For<IAgentProvider>();
        provider.Name.Returns("mock");
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "summary text" });

        var strategy = new LlmCompactionStrategy(provider);
        var summary = await strategy.SummarizeAsync(TwoMessages(), new CompactionOptions());

        await Given("a summary returned by the LLM provider", () => summary)
            .Then("the summary equals the provider response content", s =>
            {
                s.Should().Be("summary text");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Prompt sent to provider includes all conversation messages"), Fact]
    public async Task PromptIncludesMessages()
    {
        LlmRequest? capturedRequest = null;
        var provider = Substitute.For<IAgentProvider>();
        provider.Name.Returns("mock");
        provider.CompleteAsync(Arg.Do<LlmRequest>(r => capturedRequest = r), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "ok" });

        var strategy = new LlmCompactionStrategy(provider);
        var messages = new List<ConversationMessage>
        {
            new() { Role = ConversationRole.User, Content = "first" },
            new() { Role = ConversationRole.Assistant, Content = "second" }
        };
        await strategy.SummarizeAsync(messages, new CompactionOptions());

        await Given("the captured LLM request after summarization", () => capturedRequest)
            .Then("the prompt contains both message contents", req =>
            {
                req.Should().NotBeNull();
                req!.Prompt.Should().Contain("first").And.Contain("second");
                return true;
            })
            .AssertPassed();
    }
}
