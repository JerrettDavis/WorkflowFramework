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
    public async Task CompleteAsync_MapsMetadataUsageToolCallsAndSettings()
    {
        var (kernel, mockChat) = CreateMockKernel();
        var provider = new SemanticKernelAgentProvider(kernel);
        PromptExecutionSettings? capturedSettings = null;

        var toolCallMessage = new ChatMessageContent(AuthorRole.Assistant, "tool call");
        toolCallMessage.Items = new Microsoft.SemanticKernel.ChatCompletion.ChatMessageContentItemCollection
        {
            new FunctionCallContent(
                functionName: "lookup_customer",
                pluginName: "crm",
                id: "call-1",
                arguments: new KernelArguments { ["customerId"] = "42" })
        };

        var finalMessage = new ChatMessageContent(AuthorRole.Assistant, "done")
        {
            Metadata = new Dictionary<string, object?>
            {
                ["FinishReason"] = "stop",
                ["Usage"] = new
                {
                    PromptTokens = 11,
                    CompletionTokens = 7
                }
            }
        };

        mockChat.GetChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Do<PromptExecutionSettings>(s => capturedSettings = s),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>())
            .Returns([toolCallMessage, finalMessage]);

        var response = await provider.CompleteAsync(new LlmRequest
        {
            Prompt = "Look up customer",
            Temperature = 0.3,
            MaxTokens = 128
        });

        response.Content.Should().Be("done");
        response.FinishReason.Should().Be("stop");
        response.Usage.Should().NotBeNull();
        response.Usage!.PromptTokens.Should().Be(11);
        response.Usage.CompletionTokens.Should().Be(7);
        response.Usage.TotalTokens.Should().Be(18);
        response.ToolCalls.Should().ContainSingle();
        response.ToolCalls[0].ToolName.Should().Be("lookup_customer");
        response.ToolCalls[0].Arguments.Should().Contain("customerId").And.Contain("42");
        capturedSettings.Should().NotBeNull();
        capturedSettings!.ExtensionData.Should().ContainKey("temperature");
        capturedSettings.ExtensionData.Should().ContainKey("max_tokens");
    }

    [Fact]
    public async Task CompleteAsync_WithPlugins_EnablesAutoFunctionChoice()
    {
        var (kernel, mockChat) = CreateMockKernel();
        PromptExecutionSettings? capturedSettings = null;
        var provider = new SemanticKernelAgentProvider(kernel);

        kernel.Plugins.AddFromObject(new TestPlugin(), "test");

        mockChat.GetChatMessageContentsAsync(
            Arg.Any<ChatHistory>(),
            Arg.Do<PromptExecutionSettings>(s => capturedSettings = s),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, "ok")]);

        await provider.CompleteAsync(new LlmRequest { Prompt = "Use a tool if needed" });

        capturedSettings.Should().NotBeNull();
        capturedSettings!.FunctionChoiceBehavior.Should().NotBeNull();
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

    [Fact]
    public async Task DecideAsync_LongNoMatch_ReturnsFirstOptionAndIncludesContext()
    {
        var (kernel, mockChat) = CreateMockKernel();
        var provider = new SemanticKernelAgentProvider(kernel);
        ChatHistory? capturedHistory = null;

        mockChat.GetChatMessageContentsAsync(
            Arg.Do<ChatHistory>(h => capturedHistory = h),
            Arg.Any<PromptExecutionSettings>(),
            Arg.Any<Kernel>(),
            Arg.Any<CancellationToken>())
            .Returns([new ChatMessageContent(AuthorRole.Assistant, new string('x', 80))]);

        var result = await provider.DecideAsync(new AgentDecisionRequest
        {
            Prompt = "Choose the best route",
            Options = ["approve", "reject"],
            Variables = new Dictionary<string, object?>
            {
                ["priority"] = "high",
                ["ignored"] = null
            }
        });

        result.Should().Be("approve");
        capturedHistory.Should().NotBeNull();
        capturedHistory![1].Content.Should().Contain("priority").And.Contain("high");
        capturedHistory[1].Content.Should().NotContain("ignored");
    }

    private sealed class TestPlugin
    {
        [KernelFunction("echo")]
        public string Echo(string input) => input;
    }
}
#endif
