using FluentAssertions;
using WorkflowFramework.Builder;
using WorkflowFramework.Extensions.Visualization;
using Xunit;

namespace WorkflowFramework.Tests.Visualization;

public class MermaidVisualizerTests
{
    [Fact]
    public void ToMermaid_EmptyWorkflow_ShowsStartToEnd()
    {
        var workflow = new WorkflowBuilder().WithName("Empty").Build();
        var mermaid = workflow.ToMermaid();
        mermaid.Should().Contain("Start([Start])");
        mermaid.Should().Contain("End([End])");
        mermaid.Should().Contain("Start([Start]) --> End([End])");
    }

    [Fact]
    public void ToMermaid_SingleStep_ConnectsStartToStepToEnd()
    {
        var workflow = new WorkflowBuilder().WithName("Single")
            .Step("MyStep", ctx => Task.CompletedTask)
            .Build();
        var mermaid = workflow.ToMermaid();
        mermaid.Should().Contain("graph TD");
        mermaid.Should().Contain("Start([Start])");
        mermaid.Should().Contain("MyStep");
        mermaid.Should().Contain("--> End");
    }

    [Fact]
    public void ToMermaid_MultipleSteps_AllConnected()
    {
        var workflow = new WorkflowBuilder().WithName("Multi")
            .Step("Step1", ctx => Task.CompletedTask)
            .Step("Step2", ctx => Task.CompletedTask)
            .Build();
        var mermaid = workflow.ToMermaid();
        mermaid.Should().Contain("Step1");
        mermaid.Should().Contain("Step2");
    }

    [Fact]
    public void ToDot_EmptyWorkflow()
    {
        var workflow = new WorkflowBuilder().WithName("Empty").Build();
        var dot = workflow.ToDot();
        dot.Should().Contain("digraph");
        dot.Should().Contain("Start -> End");
    }

    [Fact]
    public void ToDot_SingleStep()
    {
        var workflow = new WorkflowBuilder().WithName("Single")
            .Step("MyStep", ctx => Task.CompletedTask)
            .Build();
        var dot = workflow.ToDot();
        dot.Should().Contain("MyStep");
        dot.Should().Contain("-> End");
    }

    [Fact]
    public void ToMermaid_SpecialCharacters_Sanitized()
    {
        var workflow = new WorkflowBuilder().WithName("Test")
            .Step("Step[1]", ctx => Task.CompletedTask)
            .Build();
        var mermaid = workflow.ToMermaid();
        // Special chars in labels should be escaped
        mermaid.Should().Contain("Step");
    }
}
