using FluentAssertions;
using WorkflowFramework.Extensions.Configuration;
using Xunit;

namespace WorkflowFramework.Tests.Configuration;

public class JsonWorkflowLoaderTests
{
    [Fact]
    public void Load_ValidJson_ReturnsDefinition()
    {
        var loader = new JsonWorkflowDefinitionLoader();
        var json = """{"name":"Test","version":2,"steps":[{"type":"Step1"}]}""";
        var def = loader.Load(json);
        def.Name.Should().Be("Test");
        def.Version.Should().Be(2);
        def.Steps.Should().HaveCount(1);
        def.Steps[0].Type.Should().Be("Step1");
    }

    [Fact]
    public void Load_InvalidJson_Throws()
    {
        var loader = new JsonWorkflowDefinitionLoader();
        var act = () => loader.Load("not json");
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Load_EmptyObject_ReturnsDefaults()
    {
        var loader = new JsonWorkflowDefinitionLoader();
        var def = loader.Load("{}");
        def.Name.Should().Be("Workflow");
        def.Version.Should().Be(1);
        def.Steps.Should().BeEmpty();
    }

    [Fact]
    public void Load_WithComments_AllowsTrailingCommas()
    {
        var loader = new JsonWorkflowDefinitionLoader();
        var json = """
        {
            "name": "Test",
            "steps": [
                {"type": "A",},
            ],
        }
        """;
        var def = loader.Load(json);
        def.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void WorkflowDefinition_Defaults()
    {
        var def = new WorkflowDefinition();
        def.Name.Should().Be("Workflow");
        def.Version.Should().Be(1);
        def.Steps.Should().BeEmpty();
        def.Compensation.Should().BeFalse();
    }

    [Fact]
    public void StepDefinition_Defaults()
    {
        var step = new StepDefinition();
        step.Type.Should().BeEmpty();
        step.Name.Should().BeNull();
        step.Condition.Should().BeNull();
        step.Then.Should().BeNull();
        step.Else.Should().BeNull();
        step.Retry.Should().BeNull();
        step.Parallel.Should().BeNull();
    }

    [Fact]
    public void RetryDefinition_Defaults()
    {
        var retry = new RetryDefinition();
        retry.MaxAttempts.Should().Be(3);
        retry.Backoff.Should().Be("none");
        retry.BaseDelayMs.Should().Be(100);
    }

    [Fact]
    public void LoopDefinition_Defaults()
    {
        var loop = new LoopDefinition();
        loop.Type.Should().Be("while");
        loop.MaxIterations.Should().Be(1000);
    }
}

public class StepRegistryTests
{
    [Fact]
    public void Register_AndResolve()
    {
        var registry = new StepRegistry();
        registry.Register("test", () => new TestStep());
        var step = registry.Resolve("test");
        step.Should().NotBeNull();
        step.Name.Should().Be("TestStep");
    }

    [Fact]
    public void Resolve_Unknown_Throws()
    {
        var registry = new StepRegistry();
        var act = () => registry.Resolve("unknown");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Names_ReturnsRegistered()
    {
        var registry = new StepRegistry();
        registry.Register("a", () => new TestStep());
        registry.Register("b", () => new TestStep());
        registry.Names.Should().Contain("a").And.Contain("b");
    }

    [Fact]
    public void Register_CaseInsensitive()
    {
        var registry = new StepRegistry();
        registry.Register("Test", () => new TestStep());
        var step = registry.Resolve("test");
        step.Should().NotBeNull();
    }

    private sealed class TestStep : IStep
    {
        public string Name => "TestStep";
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }
}

public class WorkflowDefinitionBuilderTests
{
    [Fact]
    public void Build_NullDefinition_Throws()
    {
        var registry = new StepRegistry();
        var builder = new WorkflowDefinitionBuilder(registry);
        var act = () => builder.Build(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Build_SimpleWorkflow()
    {
        var registry = new StepRegistry();
        registry.Register("DoSomething", () => new TestStep("DoSomething"));
        var builder = new WorkflowDefinitionBuilder(registry);
        var def = new WorkflowDefinition
        {
            Name = "TestWf",
            Steps = { new StepDefinition { Type = "DoSomething" } }
        };
        var workflow = builder.Build(def);
        workflow.Name.Should().Be("TestWf");
        workflow.Steps.Should().HaveCount(1);
    }

    private sealed class TestStep : IStep
    {
        public TestStep(string name) => Name = name;
        public string Name { get; }
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }
}
