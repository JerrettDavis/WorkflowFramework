using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.AI;
using Xunit;

namespace WorkflowFramework.Tests.Agents;

public class LlmCompactionStrategyTests
{
    [Fact]
    public void Constructor_NullProvider_Throws()
    {
        var act = () => new LlmCompactionStrategy(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Name_IsLLM()
    {
        var provider = Substitute.For<IAgentProvider>();
        var strategy = new LlmCompactionStrategy(provider);
        strategy.Name.Should().Be("LLM");
    }

    [Fact]
    public async Task SummarizeAsync_CallsProviderWithPrompt()
    {
        var provider = Substitute.For<IAgentProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "summary result" });

        var strategy = new LlmCompactionStrategy(provider);
        var messages = new List<ConversationMessage>
        {
            new() { Role = ConversationRole.User, Content = "hello" },
            new() { Role = ConversationRole.Assistant, Content = "hi there" }
        };
        var options = new CompactionOptions();

        var result = await strategy.SummarizeAsync(messages, options);

        result.Should().Be("summary result");
        await provider.Received(1).CompleteAsync(Arg.Is<LlmRequest>(r => r.Prompt.Contains("hello") && r.Prompt.Contains("hi there")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SummarizeAsync_IncludesFocusInstructions()
    {
        var provider = Substitute.For<IAgentProvider>();
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "focused summary" });

        var strategy = new LlmCompactionStrategy(provider);
        var messages = new List<ConversationMessage>
        {
            new() { Role = ConversationRole.User, Content = "msg" }
        };
        var options = new CompactionOptions { FocusInstructions = "key decisions" };

        await strategy.SummarizeAsync(messages, options);

        await provider.Received(1).CompleteAsync(Arg.Is<LlmRequest>(r => r.Prompt.Contains("key decisions")), Arg.Any<CancellationToken>());
    }
}
