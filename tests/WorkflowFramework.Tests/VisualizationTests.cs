using FluentAssertions;
using WorkflowFramework.Extensions.Visualization;
using Xunit;

namespace WorkflowFramework.Tests;

public class VisualizationTests
{
    [Fact]
    public void ToMermaid_GeneratesValidDiagram()
    {
        var workflow = Workflow.Create("Test")
            .Step("StepA", _ => Task.CompletedTask)
            .Step("StepB", _ => Task.CompletedTask)
            .Build();

        var mermaid = workflow.ToMermaid();

        mermaid.Should().Contain("graph TD");
        mermaid.Should().Contain("Start");
        mermaid.Should().Contain("End");
        mermaid.Should().Contain("StepA");
        mermaid.Should().Contain("StepB");
    }

    [Fact]
    public void ToDot_GeneratesValidGraph()
    {
        var workflow = Workflow.Create("Test")
            .Step("StepA", _ => Task.CompletedTask)
            .Step("StepB", _ => Task.CompletedTask)
            .Build();

        var dot = workflow.ToDot();

        dot.Should().Contain("digraph");
        dot.Should().Contain("StepA");
        dot.Should().Contain("StepB");
        dot.Should().Contain("Start -> ");
        dot.Should().Contain("-> End");
    }

    [Fact]
    public void ToMermaid_EmptyWorkflow_ShowsStartToEnd()
    {
        var workflow = Workflow.Create("Empty").Build();
        var mermaid = workflow.ToMermaid();

        mermaid.Should().Contain("Start([Start]) --> End([End])");
    }
}
