using FluentAssertions;
using WorkflowFramework.Extensions.AI;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.AI;

public class EchoAgentProviderTests
{
    private readonly EchoAgentProvider _provider = new();

    [Fact]
    public void Name_IsEcho() => _provider.Name.Should().Be("echo");

    [Fact]
    public async Task CompleteAsync_EchoesPrompt()
    {
        var resp = await _provider.CompleteAsync(new LlmRequest { Prompt = "Test" });
        resp.Content.Should().Be("Echo: Test");
    }

    [Fact]
    public async Task CompleteAsync_SetsFinishReason()
    {
        var resp = await _provider.CompleteAsync(new LlmRequest { Prompt = "X" });
        resp.FinishReason.Should().Be("stop");
    }

    [Fact]
    public async Task CompleteAsync_CalculatesUsage()
    {
        var resp = await _provider.CompleteAsync(new LlmRequest { Prompt = "Hello" });
        resp.Usage.Should().NotBeNull();
        resp.Usage!.PromptTokens.Should().Be(5);
        resp.Usage.CompletionTokens.Should().Be(11); // 5 + 6
        resp.Usage.TotalTokens.Should().Be(16); // 5*2 + 6
    }

    [Fact]
    public async Task DecideAsync_ReturnsFirstOption()
    {
        var result = await _provider.DecideAsync(new AgentDecisionRequest
        {
            Options = new List<string> { "A", "B", "C" }
        });
        result.Should().Be("A");
    }

    [Fact]
    public async Task DecideAsync_NoOptions_ReturnsDefault()
    {
        var result = await _provider.DecideAsync(new AgentDecisionRequest());
        result.Should().Be("default");
    }
}
