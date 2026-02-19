using FluentAssertions;
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
