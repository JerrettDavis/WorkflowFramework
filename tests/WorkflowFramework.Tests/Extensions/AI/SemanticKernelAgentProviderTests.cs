#if NET8_0_OR_GREATER
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using WorkflowFramework.Extensions.AI;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.AI;

public class SemanticKernelOptionsTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var opts = new SemanticKernelOptions();
        opts.ServiceId.Should().BeNull();
    }
}

public class SemanticKernelAgentProviderTests
{
    private static (Kernel kernel, IChatCompletionService mockChat) CreateMockKernel()
    {
        var mockChat = Substitute.For<IChatCompletionService>();
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(mockChat);
        return (builder.Build(), mockChat);
    }

    [Fact]
    public void Name_ReturnsSemanticKernel()
    {
        var (kernel, _) = CreateMockKernel();
        var provider = new SemanticKernelAgentProvider(kernel);
        provider.Name.Should().Be("SemanticKernel");
    }

    [Fact]
    public void Constructor_NullKernel_Throws()
    {
        var act = () => new SemanticKernelAgentProvider(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task CompleteAsync_ReturnsContent()
    {
        var (kernel, mockChat) = CreateMockKernel();
        var provider = new SemanticKernelAgentProvider(kernel);

        var chatMessage = new ChatMessageContent(AuthorRole.Assistant, "Hello world");
        mockChat.GetChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>())
            .Returns([chatMessage]);

        var response = await provider.CompleteAsync(new LlmRequest { Prompt = "Hi" });

        response.Content.Should().Be("Hello world");
    }

    [Fact]
    public async Task CompleteAsync_WithVariables_IncludesSystemMessage()
    {
        var (kernel, mockChat) = CreateMockKernel();
        var provider = new SemanticKernelAgentProvider(kernel);

        ChatHistory? capturedHistory = null;
        mockChat.GetChatMessageContentsAsync(
            Arg.Do<ChatHistory>(h => capturedHistory = h),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, "ok")]);

        await provider.CompleteAsync(new LlmRequest
        {
            Prompt = "test",
            Variables = new Dictionary<string, object?> { ["key"] = "value" }
        });

        capturedHistory.Should().NotBeNull();
        capturedHistory!.Should().HaveCountGreaterThanOrEqualTo(2);
        capturedHistory[0].Role.Should().Be(AuthorRole.System);
        capturedHistory[0].Content.Should().Contain("key").And.Contain("value");
    }

    [Fact]
    public async Task CompleteAsync_EmptyResult_ReturnsEmptyContent()
    {
        var (kernel, mockChat) = CreateMockKernel();
        var provider = new SemanticKernelAgentProvider(kernel);

        mockChat.GetChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<ChatMessageContent>());

        var response = await provider.CompleteAsync(new LlmRequest { Prompt = "Hi" });

        response.Content.Should().BeEmpty();
    }

    [Fact]
    public async Task DecideAsync_MatchesOption()
    {
        var (kernel, mockChat) = CreateMockKernel();
        var provider = new SemanticKernelAgentProvider(kernel);

        mockChat.GetChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, "approve")]);

        var result = await provider.DecideAsync(new AgentDecisionRequest
        {
            Prompt = "Should we approve?",
            Options = ["approve", "reject", "escalate"]
        });

        result.Should().Be("approve");
    }

    [Fact]
    public async Task DecideAsync_CaseInsensitiveMatch()
    {
        var (kernel, mockChat) = CreateMockKernel();
        var provider = new SemanticKernelAgentProvider(kernel);

        mockChat.GetChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, "REJECT")]);

        var result = await provider.DecideAsync(new AgentDecisionRequest
        {
            Prompt = "Decide",
            Options = ["approve", "reject"]
        });

        result.Should().Be("reject");
    }

    [Fact]
    public async Task DecideAsync_NoMatch_ReturnsFallback()
    {
        var (kernel, mockChat) = CreateMockKernel();
        var provider = new SemanticKernelAgentProvider(kernel);

        mockChat.GetChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, "xyz")]);

        var result = await provider.DecideAsync(new AgentDecisionRequest
        {
            Prompt = "Decide",
            Options = ["approve", "reject"]
        });

        result.Should().Be("xyz"); // short enough to return raw
    }
}
#endif
