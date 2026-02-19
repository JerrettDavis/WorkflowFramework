using System.Diagnostics;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.Agents.Diagnostics;
using WorkflowFramework.Extensions.AI;
using Xunit;

namespace WorkflowFramework.Tests.Agents;

public class AgentDiagnosticsTests : IDisposable
{
    private readonly ActivityListener _listener;
    private readonly List<Activity> _activities = new();

    public AgentDiagnosticsTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == AgentActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _activities.Add(activity)
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    private static ToolRegistry CreateRegistryWithTool(string name, string result)
    {
        var registry = new ToolRegistry();
        var provider = Substitute.For<IToolProvider>();
        provider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(new List<ToolDefinition>
        {
            new() { Name = name, Description = "test" }
        });
        provider.InvokeToolAsync(name, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ToolResult { Content = result });
        registry.Register(provider);
        return registry;
    }

    [Fact]
    public async Task AgentLoopStep_EmitsLoopAndIterationSpans()
    {
        var agentProvider = Substitute.For<IAgentProvider>();
        agentProvider.Name.Returns("TestProvider");
        agentProvider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "done" });

        var step = new AgentLoopStep(agentProvider, new ToolRegistry(), new AgentLoopOptions { StepName = "MyAgent" });
        await step.ExecuteAsync(new WorkflowContext());

        var loopActivity = _activities.Should().ContainSingle(a => a.OperationName == AgentActivitySource.AgentLoop).Subject;
        loopActivity.GetTagItem(AgentActivitySource.TagStepName).Should().Be("MyAgent");
        loopActivity.GetTagItem(AgentActivitySource.TagProviderName).Should().Be("TestProvider");
        loopActivity.GetTagItem(AgentActivitySource.TagIterationTotal).Should().Be(1);

        _activities.Should().ContainSingle(a => a.OperationName == AgentActivitySource.AgentIteration);
        var iterActivity = _activities.First(a => a.OperationName == AgentActivitySource.AgentIteration);
        iterActivity.GetTagItem(AgentActivitySource.TagIteration).Should().Be(1);
    }

    [Fact]
    public async Task AgentLoopStep_EmitsToolCallSpans()
    {
        var agentProvider = Substitute.For<IAgentProvider>();
        agentProvider.Name.Returns("Test");
        var callCount = 0;
        agentProvider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 1)
                    return new LlmResponse
                    {
                        Content = "",
                        ToolCalls = new List<ToolCall> { new() { ToolName = "search", Arguments = "{}" } }
                    };
                return new LlmResponse { Content = "done" };
            });

        var registry = CreateRegistryWithTool("search", "found it");
        var step = new AgentLoopStep(agentProvider, registry, new AgentLoopOptions());
        await step.ExecuteAsync(new WorkflowContext());

        var toolActivity = _activities.Should().ContainSingle(a => a.OperationName == AgentActivitySource.ToolCall).Subject;
        toolActivity.GetTagItem(AgentActivitySource.TagToolName).Should().Be("search");
        toolActivity.GetTagItem(AgentActivitySource.TagToolIsError).Should().Be(false);
    }

    [Fact]
    public async Task AgentLoopStep_RecordsTokenUsage()
    {
        var agentProvider = Substitute.For<IAgentProvider>();
        agentProvider.Name.Returns("Test");
        agentProvider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse
            {
                Content = "done",
                Usage = new TokenUsage { PromptTokens = 100, CompletionTokens = 50, TotalTokens = 150 }
            });

        var step = new AgentLoopStep(agentProvider, new ToolRegistry(), new AgentLoopOptions());
        await step.ExecuteAsync(new WorkflowContext());

        var iterActivity = _activities.First(a => a.OperationName == AgentActivitySource.AgentIteration);
        iterActivity.GetTagItem(AgentActivitySource.TagPromptTokens).Should().Be(100);
        iterActivity.GetTagItem(AgentActivitySource.TagCompletionTokens).Should().Be(50);
        iterActivity.GetTagItem(AgentActivitySource.TagTotalTokens).Should().Be(150);
    }

    [Fact]
    public async Task ToolCallStep_EmitsSpanWithCorrectTags()
    {
        var registry = CreateRegistryWithTool("myTool", "result");
        var step = new ToolCallStep(registry, "myTool", "{}", "TestToolStep");
        await step.ExecuteAsync(new WorkflowContext());

        var activity = _activities.Should().ContainSingle(a => a.OperationName == AgentActivitySource.ToolCall).Subject;
        activity.GetTagItem(AgentActivitySource.TagStepName).Should().Be("TestToolStep");
        activity.GetTagItem(AgentActivitySource.TagToolName).Should().Be("myTool");
        activity.GetTagItem(AgentActivitySource.TagToolIsError).Should().Be(false);
    }

    [Fact]
    public async Task ToolCallStep_ErrorSetsStatusOnSpan()
    {
        var registry = new ToolRegistry();
        var provider = Substitute.For<IToolProvider>();
        provider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(new List<ToolDefinition>
        {
            new() { Name = "failTool", Description = "fails" }
        });
        provider.InvokeToolAsync("failTool", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ToolResult { Content = "something went wrong", IsError = true });
        registry.Register(provider);

        var step = new ToolCallStep(registry, "failTool", "{}");
        await step.ExecuteAsync(new WorkflowContext());

        var activity = _activities.Should().ContainSingle(a => a.OperationName == AgentActivitySource.ToolCall).Subject;
        activity.GetTagItem(AgentActivitySource.TagToolIsError).Should().Be(true);
        activity.Status.Should().Be(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task AgentLoopStep_MultipleIterations_TracksCount()
    {
        var agentProvider = Substitute.For<IAgentProvider>();
        agentProvider.Name.Returns("Test");
        var callCount = 0;
        agentProvider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount <= 2)
                    return new LlmResponse
                    {
                        Content = "thinking",
                        ToolCalls = new List<ToolCall> { new() { ToolName = "t", Arguments = "{}" } }
                    };
                return new LlmResponse { Content = "final" };
            });

        var registry = CreateRegistryWithTool("t", "r");
        var step = new AgentLoopStep(agentProvider, registry, new AgentLoopOptions());
        await step.ExecuteAsync(new WorkflowContext());

        var loopActivity = _activities.First(a => a.OperationName == AgentActivitySource.AgentLoop);
        loopActivity.GetTagItem(AgentActivitySource.TagIterationTotal).Should().Be(3);

        _activities.Count(a => a.OperationName == AgentActivitySource.AgentIteration).Should().Be(3);
        _activities.Count(a => a.OperationName == AgentActivitySource.ToolCall).Should().Be(2);
    }
}
