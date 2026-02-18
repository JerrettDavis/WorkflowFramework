using FluentAssertions;
using WorkflowFramework.Extensions.Configuration;
using Xunit;

namespace WorkflowFramework.Tests;

public class ConfigurationTests
{
    [Fact]
    public void JsonLoader_LoadsDefinition()
    {
        var json = """
        {
            "name": "TestWorkflow",
            "version": 2,
            "steps": [
                { "type": "StepA" },
                { "type": "StepB" }
            ]
        }
        """;

        var loader = new JsonWorkflowDefinitionLoader();
        var definition = loader.Load(json);

        definition.Name.Should().Be("TestWorkflow");
        definition.Version.Should().Be(2);
        definition.Steps.Should().HaveCount(2);
        definition.Steps[0].Type.Should().Be("StepA");
        definition.Steps[1].Type.Should().Be("StepB");
    }

    [Fact]
    public void StepRegistry_RegisterAndResolve()
    {
        var registry = new StepRegistry();
        registry.Register("TestStep", () => new TestConfigStep());

        var step = registry.Resolve("TestStep");
        step.Should().BeOfType<TestConfigStep>();
    }

    [Fact]
    public void StepRegistry_ResolveUnknown_Throws()
    {
        var registry = new StepRegistry();
        var act = () => registry.Resolve("Unknown");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public async Task WorkflowDefinitionBuilder_BuildsAndExecutes()
    {
        var stepRegistry = new StepRegistry();
        stepRegistry.Register("StepA", () => new TestConfigStep());
        stepRegistry.Register("StepB", () => new TestConfigStep());

        var definition = new WorkflowDefinition
        {
            Name = "Built",
            Steps = [new StepDefinition { Type = "StepA" }, new StepDefinition { Type = "StepB" }]
        };

        var builder = new WorkflowDefinitionBuilder(stepRegistry);
        var workflow = builder.Build(definition);

        workflow.Name.Should().Be("Built");
        workflow.Steps.Should().HaveCount(2);

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);
        result.IsSuccess.Should().BeTrue();
        ((int)context.Properties["ExecutionCount"]!).Should().Be(2);
    }

    private class TestConfigStep : IStep
    {
        public string Name => "TestConfigStep";
        public Task ExecuteAsync(IWorkflowContext context)
        {
            var count = context.Properties.TryGetValue("ExecutionCount", out var v) ? (int)v! : 0;
            context.Properties["ExecutionCount"] = count + 1;
            return Task.CompletedTask;
        }
    }
}
