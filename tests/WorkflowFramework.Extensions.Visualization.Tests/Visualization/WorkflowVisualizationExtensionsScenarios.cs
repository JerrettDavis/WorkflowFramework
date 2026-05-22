using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using WorkflowFramework.Extensions.Visualization;

namespace WorkflowFramework.Extensions.Visualization.Tests.Visualization;

[Feature("WorkflowVisualizationExtensions — Mermaid and DOT export")]
public class WorkflowVisualizationExtensionsScenarios : TinyBddXunitBase
{
    public WorkflowVisualizationExtensionsScenarios(ITestOutputHelper output) : base(output) { }

    private static IWorkflow BuildWorkflow(string name, params string[] stepNames)
    {
        var builder = Workflow.Create(name);
        foreach (var stepName in stepNames)
        {
            var captured = stepName;
            builder.Step(captured, _ => Task.CompletedTask);
        }
        return builder.Build();
    }

    // ── ToMermaid ────────────────────────────────────────────────────────────

    [Scenario("ToMermaid output starts with 'graph TD'"), Fact]
    public async Task MermaidStartsWithGraphTd()
    {
        var wf = BuildWorkflow("Test", "StepA");
        var mermaid = wf.ToMermaid();

        await Given("a workflow with one step", () => mermaid)
            .Then("output starts with 'graph TD'", m =>
            {
                m.Should().StartWith("graph TD");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToMermaid includes Start and End nodes"), Fact]
    public async Task MermaidIncludesStartAndEnd()
    {
        var wf = BuildWorkflow("Test", "StepA");
        var mermaid = wf.ToMermaid();

        await Given("a single-step workflow", () => mermaid)
            .Then("output contains Start and End nodes", m =>
            {
                m.Should().Contain("Start").And.Contain("End");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToMermaid includes the step name"), Fact]
    public async Task MermaidIncludesStepName()
    {
        var wf = BuildWorkflow("Test", "ProcessOrder");
        var mermaid = wf.ToMermaid();

        await Given("a workflow with step 'ProcessOrder'", () => mermaid)
            .Then("mermaid output contains 'ProcessOrder'", m =>
            {
                m.Should().Contain("ProcessOrder");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToMermaid empty workflow produces Start --> End"), Fact]
    public async Task MermaidEmptyWorkflowProducesStartEnd()
    {
        var wf = BuildWorkflow("Empty");
        var mermaid = wf.ToMermaid();

        await Given("a workflow with no steps", () => mermaid)
            .Then("output contains direct Start --> End edge", m =>
            {
                m.Should().Contain("Start([Start]) --> End([End])");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToMermaid multiple steps are all included"), Fact]
    public async Task MermaidMultipleStepsAllIncluded()
    {
        var wf = BuildWorkflow("Multi", "Alpha", "Beta", "Gamma");
        var mermaid = wf.ToMermaid();

        await Given("a workflow with steps Alpha, Beta, Gamma", () => mermaid)
            .Then("all step names appear in the output", m =>
            {
                m.Should().Contain("Alpha").And.Contain("Beta").And.Contain("Gamma");
                return true;
            })
            .AssertPassed();
    }

    // ── ToDot ────────────────────────────────────────────────────────────────

    [Scenario("ToDot output starts with 'digraph'"), Fact]
    public async Task DotStartsWithDigraph()
    {
        var wf = BuildWorkflow("Test", "StepA");
        var dot = wf.ToDot();

        await Given("a single-step workflow", () => dot)
            .Then("DOT output starts with 'digraph'", d =>
            {
                d.Should().StartWith("digraph");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToDot output includes workflow name"), Fact]
    public async Task DotIncludesWorkflowName()
    {
        var wf = BuildWorkflow("MyFlow", "StepA");
        var dot = wf.ToDot();

        await Given("a workflow named 'MyFlow'", () => dot)
            .Then("DOT output contains 'MyFlow'", d =>
            {
                d.Should().Contain("MyFlow");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToDot empty workflow produces Start -> End"), Fact]
    public async Task DotEmptyWorkflowProducesStartEnd()
    {
        var wf = BuildWorkflow("Empty");
        var dot = wf.ToDot();

        await Given("a workflow with no steps", () => dot)
            .Then("DOT output contains 'Start -> End'", d =>
            {
                d.Should().Contain("Start -> End");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToDot includes step as a labeled node"), Fact]
    public async Task DotIncludesStepNode()
    {
        var wf = BuildWorkflow("Test", "DoWork");
        var dot = wf.ToDot();

        await Given("a workflow with step 'DoWork'", () => dot)
            .Then("DOT output contains 'DoWork' label", d =>
            {
                d.Should().Contain("DoWork");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToDot includes closing brace"), Fact]
    public async Task DotIncludesClosingBrace()
    {
        var wf = BuildWorkflow("Test", "StepX");
        var dot = wf.ToDot();

        await Given("a non-empty workflow", () => dot)
            .Then("DOT output ends with closing brace '}'", d =>
            {
                d.Trim().Should().EndWith("}");
                return true;
            })
            .AssertPassed();
    }
}
