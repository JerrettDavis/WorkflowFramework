using FluentAssertions;
using WorkflowFramework.Extensions.AI;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.AI;

public class LlmCallStepToolCallTests
{
    [Fact]
    public async Task ExecuteAsync_WithToolCalls_StoresToolCallsInContext()
    {
        var provider = new ToolCallAgentProvider();
        var step = new LlmCallStep(provider, new LlmCallOptions { PromptTemplate = "test" });

        var ctx = new TestContext();
        await step.ExecuteAsync(ctx);

        ctx.Properties.Should().ContainKey("LlmCall.ToolCalls");
        var toolCalls = (IList<ToolCall>)ctx.Properties["LlmCall.ToolCalls"]!;
        toolCalls.Should().HaveCount(1);
        toolCalls[0].ToolName.Should().Be("search");
    }

    [Fact]
    public async Task ExecuteAsync_NoToolCalls_DoesNotStoreToolCalls()
    {
        var step = new LlmCallStep(new EchoAgentProvider(), new LlmCallOptions { PromptTemplate = "test" });
        var ctx = new TestContext();
        await step.ExecuteAsync(ctx);
        ctx.Properties.Should().NotContainKey("LlmCall.ToolCalls");
    }

    [Fact]
    public async Task ExecuteAsync_NullUsage_DoesNotStoreTotalTokens()
    {
        var provider = new NullUsageProvider();
        var step = new LlmCallStep(provider, new LlmCallOptions { PromptTemplate = "test" });
        var ctx = new TestContext();
        await step.ExecuteAsync(ctx);
        ctx.Properties.Should().NotContainKey("LlmCall.TotalTokens");
    }

    private class ToolCallAgentProvider : IAgentProvider
    {
        public string Name => "toolcall";
        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
        {
            var response = new LlmResponse
            {
                Content = "calling tool",
                FinishReason = "tool_calls",
                Usage = new TokenUsage { TotalTokens = 10 },
                ToolCalls = new List<ToolCall> { new() { ToolName = "search", Arguments = "{}" } }
            };
            return Task.FromResult(response);
        }
        public Task<string> DecideAsync(AgentDecisionRequest request, CancellationToken ct = default)
            => Task.FromResult("default");
    }

    private class NullUsageProvider : IAgentProvider
    {
        public string Name => "nullusage";
        public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken ct = default)
            => Task.FromResult(new LlmResponse { Content = "ok", Usage = null });
        public Task<string> DecideAsync(AgentDecisionRequest request, CancellationToken ct = default)
            => Task.FromResult("default");
    }

    private class TestContext : IWorkflowContext
    {
        public string WorkflowId { get; set; } = "w";
        public string CorrelationId { get; set; } = "c";
        public CancellationToken CancellationToken { get; set; }
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
        public string? CurrentStepName { get; set; }
        public int CurrentStepIndex { get; set; }
        public bool IsAborted { get; set; }
        public IList<WorkflowError> Errors { get; } = new List<WorkflowError>();
    }
}
