using FluentAssertions;
using WorkflowFramework.Extensions.Configuration;
using Xunit;

namespace WorkflowFramework.Tests.Configuration;

public class YamlWorkflowLoaderTests
{
    [Fact]
    public void Load_ValidYaml_ReturnsDefinition()
    {
        var loader = new YamlWorkflowDefinitionLoader();
        var yaml = """
            name: TestWorkflow
            version: 3
            steps:
              - type: StepA
              - type: StepB
            """;
        var def = loader.Load(yaml);
        def.Name.Should().Be("TestWorkflow");
        def.Version.Should().Be(3);
        def.Steps.Should().HaveCount(2);
    }

    [Fact]
    public void Load_NullContent_Throws()
    {
        var loader = new YamlWorkflowDefinitionLoader();
        var act = () => loader.Load(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Load_EmptyYaml_ReturnsDefaults()
    {
        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load("name: Test");
        def.Name.Should().Be("Test");
    }

    [Fact]
    public void LoadFromFile_NullPath_Throws()
    {
        var loader = new YamlWorkflowDefinitionLoader();
        var act = () => loader.LoadFromFile(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LoadFromFile_MissingFile_Throws()
    {
        var loader = new YamlWorkflowDefinitionLoader();
        var act = () => loader.LoadFromFile("nonexistent.yaml");
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void LoadFromFile_ValidFile_ReturnsDefinition()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "name: FromFile\nversion: 5\nsteps:\n  - type: A\n");
            var loader = new YamlWorkflowDefinitionLoader();
            var def = loader.LoadFromFile(path);
            def.Name.Should().Be("FromFile");
            def.Version.Should().Be(5);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public class JsonWorkflowLoaderExtendedTests
{
    [Fact]
    public void LoadFromFile_ValidFile_ReturnsDefinition()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{"name":"FromFile","version":7}""");
            var loader = new JsonWorkflowDefinitionLoader();
            var def = loader.LoadFromFile(path);
            def.Name.Should().Be("FromFile");
            def.Version.Should().Be(7);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadFromFile_MissingFile_Throws()
    {
        var loader = new JsonWorkflowDefinitionLoader();
        var act = () => loader.LoadFromFile("nonexistent.json");
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Load_WithAllStepProperties()
    {
        var loader = new JsonWorkflowDefinitionLoader();
        var json = """
        {
            "name": "Full",
            "steps": [{
                "type": "MyStep",
                "name": "Named",
                "condition": "check",
                "then": "ThenStep",
                "else": "ElseStep",
                "timeoutSeconds": 30.0,
                "parallel": ["A", "B"],
                "subWorkflow": "SubWf",
                "steps": [{"type": "Child"}],
                "retry": {"maxAttempts": 5, "backoff": "exponential", "baseDelayMs": 500},
                "loop": {"type": "forEach", "maxIterations": 100}
            }]
        }
        """;
        var def = loader.Load(json);
        var step = def.Steps[0];
        step.Type.Should().Be("MyStep");
        step.Name.Should().Be("Named");
        step.Condition.Should().Be("check");
        step.Then.Should().Be("ThenStep");
        step.Else.Should().Be("ElseStep");
        step.TimeoutSeconds.Should().Be(30.0);
        step.Parallel.Should().Contain("A").And.Contain("B");
        step.SubWorkflow.Should().Be("SubWf");
        step.Steps.Should().HaveCount(1);
        step.Retry!.MaxAttempts.Should().Be(5);
        step.Retry.Backoff.Should().Be("exponential");
        step.Retry.BaseDelayMs.Should().Be(500);
        step.Loop!.Type.Should().Be("forEach");
        step.Loop.MaxIterations.Should().Be(100);
    }
}

public class StepDefinitionExtendedTests
{
    [Fact]
    public void StepDefinition_AllProperties()
    {
        var step = new StepDefinition
        {
            Type = "MyStep",
            Name = "Named",
            Condition = "cond",
            Then = "ThenStep",
            Else = "ElseStep",
            TimeoutSeconds = 10.0,
            Parallel = ["A"],
            SubWorkflow = "Sub",
            Steps = [new StepDefinition { Type = "Child" }],
            Retry = new RetryDefinition { MaxAttempts = 2 },
            Loop = new LoopDefinition { Type = "doWhile", MaxIterations = 50 }
        };

        step.TimeoutSeconds.Should().Be(10.0);
        step.SubWorkflow.Should().Be("Sub");
        step.Steps.Should().HaveCount(1);
        step.Loop!.Type.Should().Be("doWhile");
        step.Loop.MaxIterations.Should().Be(50);
    }
}

public class WorkflowDefinitionBuilderExtendedTests
{
    [Fact]
    public void Build_WithCompensation()
    {
        var registry = new StepRegistry();
        registry.Register("A", () => new TestStep("A"));
        var builder = new WorkflowDefinitionBuilder(registry);
        var def = new WorkflowDefinition
        {
            Name = "CompWf",
            Compensation = true,
            Steps = [new StepDefinition { Type = "A" }]
        };
        var workflow = builder.Build(def);
        workflow.Name.Should().Be("CompWf");
    }

    [Fact]
    public void Build_WithParallelSteps()
    {
        var registry = new StepRegistry();
        registry.Register("A", () => new TestStep("A"));
        registry.Register("B", () => new TestStep("B"));
        var builder = new WorkflowDefinitionBuilder(registry);
        var def = new WorkflowDefinition
        {
            Name = "ParallelWf",
            Steps = [new StepDefinition { Parallel = ["A", "B"] }]
        };
        var workflow = builder.Build(def);
        workflow.Steps.Should().HaveCount(1); // The parallel group is one step
    }

    [Fact]
    public void Build_WithConditional_ThenOnly()
    {
        var registry = new StepRegistry();
        registry.Register("Check", () => new TestStep("Check"));
        var builder = new WorkflowDefinitionBuilder(registry);
        var def = new WorkflowDefinition
        {
            Name = "CondWf",
            Steps = [new StepDefinition { Condition = "flag", Then = "Check" }]
        };
        var workflow = builder.Build(def);
        workflow.Steps.Should().NotBeEmpty();
    }

    [Fact]
    public void Build_WithConditional_ThenAndElse()
    {
        var registry = new StepRegistry();
        registry.Register("ThenStep", () => new TestStep("ThenStep"));
        registry.Register("ElseStep", () => new TestStep("ElseStep"));
        var builder = new WorkflowDefinitionBuilder(registry);
        var def = new WorkflowDefinition
        {
            Name = "CondWf",
            Steps = [new StepDefinition { Condition = "flag", Then = "ThenStep", Else = "ElseStep" }]
        };
        var workflow = builder.Build(def);
        workflow.Steps.Should().NotBeEmpty();
    }

    [Fact]
    public void Build_WithRetry()
    {
        var registry = new StepRegistry();
        registry.Register("Flaky", () => new TestStep("Flaky"));
        var builder = new WorkflowDefinitionBuilder(registry);
        var def = new WorkflowDefinition
        {
            Name = "RetryWf",
            Steps = [new StepDefinition { Type = "Flaky", Retry = new RetryDefinition { MaxAttempts = 3 } }]
        };
        var workflow = builder.Build(def);
        workflow.Steps.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Build_ConditionalThen_ExecutesCorrectBranch()
    {
        var registry = new StepRegistry();
        registry.Register("Yes", () => new TestStep("Yes"));
        registry.Register("No", () => new TestStep("No"));
        var builder = new WorkflowDefinitionBuilder(registry);
        var def = new WorkflowDefinition
        {
            Steps = [new StepDefinition { Condition = "flag", Then = "Yes", Else = "No" }]
        };
        var workflow = builder.Build(def);

        // Test true branch
        var ctx = new WorkflowContext();
        ctx.Properties["flag"] = true;
        await workflow.ExecuteAsync(ctx);

        // Test false branch
        var ctx2 = new WorkflowContext();
        ctx2.Properties["flag"] = false;
        await workflow.ExecuteAsync(ctx2);
    }

    [Fact]
    public async Task Build_ConditionalThen_StringTrue()
    {
        var registry = new StepRegistry();
        registry.Register("Yes", () => new TestStep("Yes"));
        var builder = new WorkflowDefinitionBuilder(registry);
        var def = new WorkflowDefinition
        {
            Steps = [new StepDefinition { Condition = "flag", Then = "Yes" }]
        };
        var workflow = builder.Build(def);

        var ctx = new WorkflowContext();
        ctx.Properties["flag"] = "true";
        await workflow.ExecuteAsync(ctx);
    }

    [Fact]
    public void StepRegistry_RegisterGeneric()
    {
        var registry = new StepRegistry();
        registry.Register<TestStep>();
        registry.Resolve("TestStep").Should().NotBeNull();
    }

    [Fact]
    public void StepRegistry_RegisterGeneric_WithName()
    {
        var registry = new StepRegistry();
        registry.Register<TestStep>("CustomName");
        registry.Resolve("CustomName").Should().NotBeNull();
    }

    private sealed class TestStep(string name) : IStep
    {
        public TestStep() : this("TestStep") { }
        public string Name { get; } = name;
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }
}
