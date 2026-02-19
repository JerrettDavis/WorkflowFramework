using System.Diagnostics;
using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.Agents.Diagnostics;
using WorkflowFramework.Extensions.AI;
using Xunit;

namespace WorkflowFramework.Tests.Agents;

/// <summary>
/// Each test uses unique names and filters captured activities by those names
/// to avoid cross-contamination from parallel test execution.
/// </summary>
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

    private List<Activity> ActivitiesForStep(string stepName) =>
        _activities.Where(a => a.GetTagItem(AgentActivitySource.TagStepName) as string == stepName).ToList();

    private List<Activity> ActivitiesForTool(string toolName) =>
        _activities.Where(a => a.GetTagItem(AgentActivitySource.TagToolName) as string == toolName).ToList();

    [Fact]
    public async Task AgentLoopStep_EmitsLoopAndIterationSpans()
    {
        var stepName = $"MyAgent_{Guid.NewGuid():N}";
        var agentProvider = Substitute.For<IAgentProvider>();
        agentProvider.Name.Returns("TestProvider");
        agentProvider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse { Content = "done" });

        var step = new AgentLoopStep(agentProvider, new ToolRegistry(), new AgentLoopOptions { StepName = stepName });
        await step.ExecuteAsync(new WorkflowContext());

        var myActivities = ActivitiesForStep(stepName);
        var loopActivity = myActivities.Should().ContainSingle(a => a.OperationName == AgentActivitySource.AgentLoop).Subject;
        loopActivity.GetTagItem(AgentActivitySource.TagProviderName).Should().Be("TestProvider");
        loopActivity.GetTagItem(AgentActivitySource.TagIterationTotal).Should().Be(1);

        myActivities.Should().ContainSingle(a => a.OperationName == AgentActivitySource.AgentIteration);
    }

    [Fact]
    public async Task AgentLoopStep_EmitsToolCallSpans()
    {
        var toolName = $"search_{Guid.NewGuid():N}";
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
                        ToolCalls = new List<ToolCall> { new() { ToolName = toolName, Arguments = "{}" } }
                    };
                return new LlmResponse { Content = "done" };
            });

        var registry = CreateRegistryWithTool(toolName, "found it");
        var step = new AgentLoopStep(agentProvider, registry, new AgentLoopOptions());
        await step.ExecuteAsync(new WorkflowContext());

        var toolActivity = ActivitiesForTool(toolName).Should().ContainSingle().Subject;
        toolActivity.OperationName.Should().Be(AgentActivitySource.ToolCall);
        toolActivity.GetTagItem(AgentActivitySource.TagToolIsError).Should().Be(false);
    }

    [Fact]
    public async Task AgentLoopStep_RecordsTokenUsage()
    {
        var stepName = $"TokenTest_{Guid.NewGuid():N}";
        var agentProvider = Substitute.For<IAgentProvider>();
        agentProvider.Name.Returns("Test");
        agentProvider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(new LlmResponse
            {
                Content = "done",
                Usage = new TokenUsage { PromptTokens = 100, CompletionTokens = 50, TotalTokens = 150 }
            });

        var step = new AgentLoopStep(agentProvider, new ToolRegistry(), new AgentLoopOptions { StepName = stepName });
        await step.ExecuteAsync(new WorkflowContext());

        var iterActivity = ActivitiesForStep(stepName).First(a => a.OperationName == AgentActivitySource.AgentIteration);
        iterActivity.GetTagItem(AgentActivitySource.TagPromptTokens).Should().Be(100);
        iterActivity.GetTagItem(AgentActivitySource.TagCompletionTokens).Should().Be(50);
        iterActivity.GetTagItem(AgentActivitySource.TagTotalTokens).Should().Be(150);
    }

    [Fact]
    public async Task ToolCallStep_EmitsSpanWithCorrectTags()
    {
        var toolName = $"myTool_{Guid.NewGuid():N}";
        var registry = CreateRegistryWithTool(toolName, "result");
        var step = new ToolCallStep(registry, toolName, "{}", "TestToolStep");
        await step.ExecuteAsync(new WorkflowContext());

        var activity = ActivitiesForTool(toolName).Should().ContainSingle().Subject;
        activity.OperationName.Should().Be(AgentActivitySource.ToolCall);
        activity.GetTagItem(AgentActivitySource.TagStepName).Should().Be("TestToolStep");
        activity.GetTagItem(AgentActivitySource.TagToolIsError).Should().Be(false);
    }

    [Fact]
    public async Task ToolCallStep_ErrorSetsStatusOnSpan()
    {
        var toolName = $"failTool_{Guid.NewGuid():N}";
        var registry = new ToolRegistry();
        var provider = Substitute.For<IToolProvider>();
        provider.ListToolsAsync(Arg.Any<CancellationToken>()).Returns(new List<ToolDefinition>
        {
            new() { Name = toolName, Description = "fails" }
        });
        provider.InvokeToolAsync(toolName, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ToolResult { Content = "something went wrong", IsError = true });
        registry.Register(provider);

        var step = new ToolCallStep(registry, toolName, "{}");
        await step.ExecuteAsync(new WorkflowContext());

        var activity = ActivitiesForTool(toolName).Should().ContainSingle().Subject;
        activity.GetTagItem(AgentActivitySource.TagToolIsError).Should().Be(true);
        activity.Status.Should().Be(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task AgentLoopStep_MultipleIterations_TracksCount()
    {
        var stepName = $"MultiIter_{Guid.NewGuid():N}";
        var toolName = $"t_{Guid.NewGuid():N}";
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
                        ToolCalls = new List<ToolCall> { new() { ToolName = toolName, Arguments = "{}" } }
                    };
                return new LlmResponse { Content = "final" };
            });

        var registry = CreateRegistryWithTool(toolName, "r");
        var step = new AgentLoopStep(agentProvider, registry, new AgentLoopOptions { StepName = stepName });
        await step.ExecuteAsync(new WorkflowContext());

        var myActivities = ActivitiesForStep(stepName);
        var loopActivity = myActivities.First(a => a.OperationName == AgentActivitySource.AgentLoop);
        loopActivity.GetTagItem(AgentActivitySource.TagIterationTotal).Should().Be(3);

        myActivities.Count(a => a.OperationName == AgentActivitySource.AgentIteration).Should().Be(3);
        ActivitiesForTool(toolName).Should().HaveCount(2);
    }
}
