using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.AI;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.AI;

public class LlmCallStepTests
{
    [Fact]
    public void Constructor_NullProvider_Throws()
    {
        FluentActions.Invoking(() => new LlmCallStep(null!, new LlmCallOptions()))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        FluentActions.Invoking(() => new LlmCallStep(new EchoAgentProvider(), null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Name_Default() => new LlmCallStep(new EchoAgentProvider(), new LlmCallOptions()).Name.Should().Be("LlmCall");

    [Fact]
    public void Name_Custom() => new LlmCallStep(new EchoAgentProvider(), new LlmCallOptions { StepName = "AI" }).Name.Should().Be("AI");

    [Fact]
    public async Task ExecuteAsync_StoresResponseAndUsage()
    {
        var step = new LlmCallStep(new EchoAgentProvider(), new LlmCallOptions { PromptTemplate = "Hi" });
        var ctx = CreateCtx();
        await step.ExecuteAsync(ctx);
        ctx.Properties["LlmCall.Response"].Should().Be("Echo: Hi");
        ctx.Properties["LlmCall.FinishReason"].Should().Be("stop");
        ctx.Properties.Should().ContainKey("LlmCall.TotalTokens");
    }

    [Fact]
    public async Task ExecuteAsync_RendersPromptTemplateWithWorkflowProperties()
    {
        var provider = Substitute.For<IAgentProvider>();
        LlmRequest? captured = null;
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<LlmRequest>();
                return new LlmResponse { Content = "done" };
            });

        var step = new LlmCallStep(provider, new LlmCallOptions { PromptTemplate = "Hello {Name}" });
        var ctx = CreateCtx();
        ctx.Properties["Name"] = "Ada";

        await step.ExecuteAsync(ctx);

        captured.Should().NotBeNull();
        captured!.Prompt.Should().Be("Hello Ada");
    }

    [Fact]
    public void LlmCallOptions_Defaults()
    {
        var o = new LlmCallOptions();
        o.StepName.Should().BeNull();
        o.PromptTemplate.Should().BeEmpty();
        o.Model.Should().BeNull();
        o.Temperature.Should().BeNull();
        o.MaxTokens.Should().BeNull();
        o.Tools.Should().BeEmpty();
    }

    private static IWorkflowContext CreateCtx() => new Ctx();
    private class Ctx : IWorkflowContext
    {
        public string WorkflowId { get; set; } = "w"; public string CorrelationId { get; set; } = "c";
        public CancellationToken CancellationToken { get; set; }
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
        public string? CurrentStepName { get; set; } public int CurrentStepIndex { get; set; }
        public bool IsAborted { get; set; } public IList<WorkflowError> Errors { get; } = new List<WorkflowError>();
    }
}

public class AgentDecisionStepTests
{
    [Fact]
    public void Constructor_NullProvider_Throws()
    {
        FluentActions.Invoking(() => new AgentDecisionStep(null!, new AgentDecisionOptions()))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        FluentActions.Invoking(() => new AgentDecisionStep(new EchoAgentProvider(), null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Name_Default() => new AgentDecisionStep(new EchoAgentProvider(), new AgentDecisionOptions()).Name.Should().Be("AgentDecision");

    [Fact]
    public async Task ExecuteAsync_StoresDecision()
    {
        var step = new AgentDecisionStep(new EchoAgentProvider(), new AgentDecisionOptions
        {
            Options = new List<string> { "A", "B" }
        });
        var ctx = new LlmCallStepTests().GetType().Assembly.GetTypes(); // just need a context
        var context = CreateCtx();
        await step.ExecuteAsync(context);
        context.Properties["AgentDecision.Decision"].Should().Be("A");
    }

    [Fact]
    public async Task ExecuteAsync_RendersDecisionPromptTemplate()
    {
        var provider = Substitute.For<IAgentProvider>();
        AgentDecisionRequest? captured = null;
        provider.DecideAsync(Arg.Any<AgentDecisionRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<AgentDecisionRequest>();
                return "RouteA";
            });

        var step = new AgentDecisionStep(provider, new AgentDecisionOptions
        {
            Prompt = "Choose route for {OrderId}",
            Options = new List<string> { "RouteA", "RouteB" }
        });
        var context = CreateCtx();
        context.Properties["OrderId"] = "ORD-42";

        await step.ExecuteAsync(context);

        captured.Should().NotBeNull();
        captured!.Prompt.Should().Be("Choose route for ORD-42");
        context.Properties["AgentDecision.Decision"].Should().Be("RouteA");
    }

    [Fact]
    public void AgentDecisionOptions_Defaults()
    {
        var o = new AgentDecisionOptions();
        o.StepName.Should().BeNull();
        o.Prompt.Should().BeEmpty();
        o.Options.Should().BeEmpty();
    }

    private static IWorkflowContext CreateCtx() => new Ctx();
    private class Ctx : IWorkflowContext
    {
        public string WorkflowId { get; set; } = "w"; public string CorrelationId { get; set; } = "c";
        public CancellationToken CancellationToken { get; set; }
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
        public string? CurrentStepName { get; set; } public int CurrentStepIndex { get; set; }
        public bool IsAborted { get; set; } public IList<WorkflowError> Errors { get; } = new List<WorkflowError>();
    }
}

public class AgentPlanStepTests
{
    [Fact]
    public void Constructor_NullProvider_Throws()
    {
        FluentActions.Invoking(() => new AgentPlanStep(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Name_Default() => new AgentPlanStep(new EchoAgentProvider()).Name.Should().Be("AgentPlan");

    [Fact]
    public void Name_Custom() => new AgentPlanStep(new EchoAgentProvider(), "MyPlan").Name.Should().Be("MyPlan");

    [Fact]
    public async Task ExecuteAsync_StoresPlan()
    {
        var step = new AgentPlanStep(new EchoAgentProvider());
        var ctx = CreateCtx();
        await step.ExecuteAsync(ctx);
        ((string)ctx.Properties["AgentPlan.Plan"]!).Should().StartWith("Echo:");
    }

    [Fact]
    public async Task ExecuteAsync_UsesConfiguredPlanOptions()
    {
        var provider = Substitute.For<IAgentProvider>();
        LlmRequest? captured = null;
        provider.CompleteAsync(Arg.Any<LlmRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<LlmRequest>();
                return new LlmResponse
                {
                    Content = "Plan: investigate then notify",
                    FinishReason = "stop",
                    Usage = new TokenUsage { PromptTokens = 5, CompletionTokens = 7, TotalTokens = 12 }
                };
            });

        var step = new AgentPlanStep(provider, new AgentPlanOptions
        {
            StepName = "Planner",
            PromptTemplate = "Plan next steps for {IncidentId}",
            Model = "planner-model",
            Temperature = 0.2,
            MaxTokens = 256,
            OutputPropertyName = "Workflow.Plan"
        });

        var ctx = CreateCtx();
        ctx.Properties["IncidentId"] = "INC-9";

        await step.ExecuteAsync(ctx);

        captured.Should().NotBeNull();
        captured!.Prompt.Should().Be("Plan next steps for INC-9");
        captured.Model.Should().Be("planner-model");
        captured.Temperature.Should().Be(0.2);
        captured.MaxTokens.Should().Be(256);
        ctx.Properties["Workflow.Plan"].Should().Be("Plan: investigate then notify");
        ctx.Properties["Planner.FinishReason"].Should().Be("stop");
        ctx.Properties["Planner.TotalTokens"].Should().Be(12);
    }

    [Fact]
    public void AgentPlanOptions_Defaults()
    {
        var options = new AgentPlanOptions();
        options.StepName.Should().BeNull();
        options.PromptTemplate.Should().Be("Given the current workflow state, suggest the next steps to take.");
        options.Model.Should().BeNull();
        options.Temperature.Should().BeNull();
        options.MaxTokens.Should().BeNull();
        options.OutputPropertyName.Should().BeNull();
    }

    private static IWorkflowContext CreateCtx() => new Ctx();
    private class Ctx : IWorkflowContext
    {
        public string WorkflowId { get; set; } = "w"; public string CorrelationId { get; set; } = "c";
        public CancellationToken CancellationToken { get; set; }
        public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
        public string? CurrentStepName { get; set; } public int CurrentStepIndex { get; set; }
        public bool IsAborted { get; set; } public IList<WorkflowError> Errors { get; } = new List<WorkflowError>();
    }
}
