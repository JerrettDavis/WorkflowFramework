using FluentAssertions;
using WorkflowFramework.Extensions.Agents;
using WorkflowFramework.Extensions.AI;
using Xunit;

namespace WorkflowFramework.Tests.Agents;

public class AgentBuilderAiStepTests
{
    [Fact]
    public void LlmCall_AddsStep()
    {
        var builder = Workflow.Create("test");
        builder.LlmCall(new EchoAgentProvider(), options => options.PromptTemplate = "Hello {Name}");

        var workflow = builder.Build();

        workflow.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void AgentDecision_AddsStep()
    {
        var builder = Workflow.Create("test");
        builder.AgentDecision(new EchoAgentProvider(), options =>
        {
            options.Prompt = "Choose route";
            options.Options = new List<string> { "A", "B" };
        });

        var workflow = builder.Build();

        workflow.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void AgentPlan_AddsStep()
    {
        var builder = Workflow.Create("test");
        builder.AgentPlan(new EchoAgentProvider(), options => options.StepName = "Planner");

        var workflow = builder.Build();

        workflow.Steps.Should().HaveCount(1);
    }
}
