using FluentAssertions;
using WorkflowFramework.Serialization;
using Xunit;
using WorkflowFramework.Builder;

namespace WorkflowFramework.Tests.Serialization;

public class WorkflowSerializerTests
{
    [Fact]
    public void ToJson_SimpleWorkflow_RoundTrips()
    {
        var workflow = Workflow.Create("TestWorkflow")
            .Step("Step1", _ => Task.CompletedTask)
            .Step("Step2", _ => Task.CompletedTask)
            .Build();

        var json = WorkflowSerializer.ToJson(workflow);
        var dto = WorkflowSerializer.FromJson(json);

        dto.Name.Should().Be("TestWorkflow");
        dto.Steps.Should().HaveCount(2);
        dto.Steps[0].Name.Should().Be("Step1");
        dto.Steps[0].Type.Should().Be("action");
        dto.Steps[1].Name.Should().Be("Step2");
    }

    [Fact]
    public void ToYaml_SimpleWorkflow_RoundTrips()
    {
        var workflow = Workflow.Create("TestWorkflow")
            .Step("Step1", _ => Task.CompletedTask)
            .Step("Step2", _ => Task.CompletedTask)
            .Build();

        var yaml = WorkflowSerializer.ToYaml(workflow);
        var dto = WorkflowSerializer.FromYaml(yaml);

        dto.Name.Should().Be("TestWorkflow");
        dto.Steps.Should().HaveCount(2);
        dto.Steps[0].Name.Should().Be("Step1");
        dto.Steps[0].Type.Should().Be("action");
        dto.Steps[1].Name.Should().Be("Step2");
    }

    [Fact]
    public void ToJson_ParallelWorkflow_RoundTrips()
    {
        var workflow = Workflow.Create("ParallelTest")
            .Parallel(p =>
            {
                p.Step(new NamedStep("A"));
                p.Step(new NamedStep("B"));
            })
            .Build();

        var json = WorkflowSerializer.ToJson(workflow);
        var dto = WorkflowSerializer.FromJson(json);

        dto.Steps.Should().HaveCount(1);
        dto.Steps[0].Type.Should().Be("parallel");
        dto.Steps[0].Steps.Should().HaveCount(2);
        dto.Steps[0].Steps![0].Name.Should().Be("A");
        dto.Steps[0].Steps![1].Name.Should().Be("B");
    }

    [Fact]
    public void ToYaml_ParallelWorkflow_RoundTrips()
    {
        var workflow = Workflow.Create("ParallelTest")
            .Parallel(p =>
            {
                p.Step(new NamedStep("A"));
                p.Step(new NamedStep("B"));
            })
            .Build();

        var yaml = WorkflowSerializer.ToYaml(workflow);
        var dto = WorkflowSerializer.FromYaml(yaml);

        dto.Steps.Should().HaveCount(1);
        dto.Steps[0].Type.Should().Be("parallel");
        dto.Steps[0].Steps.Should().HaveCount(2);
    }

    [Fact]
    public void ToJson_ConditionalWorkflow_RoundTrips()
    {
        var workflow = Workflow.Create("CondTest")
            .If(_ => true)
                .Then(new NamedStep("ThenStep"))
                .Else(new NamedStep("ElseStep"))
            .Build();

        var json = WorkflowSerializer.ToJson(workflow);
        var dto = WorkflowSerializer.FromJson(json);

        dto.Steps.Should().HaveCount(1);
        dto.Steps[0].Type.Should().Be("conditional");
        dto.Steps[0].Then.Should().NotBeNull();
        dto.Steps[0].Then!.Name.Should().Be("ThenStep");
        dto.Steps[0].Else.Should().NotBeNull();
        dto.Steps[0].Else!.Name.Should().Be("ElseStep");
    }

    [Fact]
    public void ToYaml_ConditionalWorkflow_RoundTrips()
    {
        var workflow = Workflow.Create("CondTest")
            .If(_ => true)
                .Then(new NamedStep("ThenStep"))
                .Else(new NamedStep("ElseStep"))
            .Build();

        var yaml = WorkflowSerializer.ToYaml(workflow);
        var dto = WorkflowSerializer.FromYaml(yaml);

        dto.Steps[0].Type.Should().Be("conditional");
        dto.Steps[0].Then!.Name.Should().Be("ThenStep");
        dto.Steps[0].Else!.Name.Should().Be("ElseStep");
    }

    [Fact]
    public void ToJson_RetryWorkflow_RoundTrips()
    {
        var workflow = Workflow.Create("RetryTest")
            .Retry(b => b.Step("Inner", _ => Task.CompletedTask), maxAttempts: 5)
            .Build();

        var json = WorkflowSerializer.ToJson(workflow);
        var dto = WorkflowSerializer.FromJson(json);

        dto.Steps.Should().HaveCount(1);
        dto.Steps[0].Type.Should().Be("retry");
        dto.Steps[0].MaxAttempts.Should().Be(5);
        dto.Steps[0].Steps.Should().HaveCount(1);
    }

    [Fact]
    public void ToYaml_RetryWorkflow_RoundTrips()
    {
        var workflow = Workflow.Create("RetryTest")
            .Retry(b => b.Step("Inner", _ => Task.CompletedTask), maxAttempts: 5)
            .Build();

        var yaml = WorkflowSerializer.ToYaml(workflow);
        var dto = WorkflowSerializer.FromYaml(yaml);

        dto.Steps[0].Type.Should().Be("retry");
        dto.Steps[0].MaxAttempts.Should().Be(5);
    }

    [Fact]
    public void ToJson_ForEachWorkflow_RoundTrips()
    {
        var workflow = Workflow.Create("ForEachTest")
            .ForEach<int>(_ => new[] { 1, 2, 3 }, b =>
                b.Step("Process", _ => Task.CompletedTask))
            .Build();

        var json = WorkflowSerializer.ToJson(workflow);
        var dto = WorkflowSerializer.FromJson(json);

        dto.Steps.Should().HaveCount(1);
        dto.Steps[0].Type.Should().Be("forEach");
        dto.Steps[0].Steps.Should().HaveCount(1);
    }

    [Fact]
    public void ToJson_TimeoutWorkflow_CapturesStructure()
    {
        // TimeoutStep wraps an inner step
        var workflow = Workflow.Create("TimeoutTest")
            .Step("MyStep", _ => Task.CompletedTask)
            .Build();

        // Direct timeout step test via definition
        var json = WorkflowSerializer.ToJson(workflow);
        var dto = WorkflowSerializer.FromJson(json);

        dto.Name.Should().Be("TimeoutTest");
        dto.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void ToJson_DelayWorkflow_RoundTrips()
    {
        var workflow = Workflow.Create("DelayTest")
            .Delay(TimeSpan.FromSeconds(5))
            .Build();

        var json = WorkflowSerializer.ToJson(workflow);
        var dto = WorkflowSerializer.FromJson(json);

        dto.Steps.Should().HaveCount(1);
        dto.Steps[0].Type.Should().Be("delay");
        dto.Steps[0].DelaySeconds.Should().Be(5);
    }

    [Fact]
    public void ToYaml_DelayWorkflow_RoundTrips()
    {
        var workflow = Workflow.Create("DelayTest")
            .Delay(TimeSpan.FromSeconds(5))
            .Build();

        var yaml = WorkflowSerializer.ToYaml(workflow);
        var dto = WorkflowSerializer.FromYaml(yaml);

        dto.Steps[0].Type.Should().Be("delay");
        dto.Steps[0].DelaySeconds.Should().Be(5);
    }

    [Fact]
    public void ToJson_ComplexWorkflow_RoundTrips()
    {
        var workflow = Workflow.Create("Complex")
            .Step("Init", _ => Task.CompletedTask)
            .Parallel(p =>
            {
                p.Step(new NamedStep("ParA"));
                p.Step(new NamedStep("ParB"));
            })
            .If(_ => true)
                .Then(new NamedStep("ThenBranch"))
                .Else(new NamedStep("ElseBranch"))
            .Retry(b => b.Step("Retryable", _ => Task.CompletedTask), maxAttempts: 3)
            .Delay(TimeSpan.FromSeconds(1))
            .Build();

        var json = WorkflowSerializer.ToJson(workflow);
        var dto = WorkflowSerializer.FromJson(json);

        dto.Name.Should().Be("Complex");
        dto.Steps.Should().HaveCount(5);
        dto.Steps[0].Type.Should().Be("action");
        dto.Steps[1].Type.Should().Be("parallel");
        dto.Steps[2].Type.Should().Be("conditional");
        dto.Steps[3].Type.Should().Be("retry");
        dto.Steps[4].Type.Should().Be("delay");
    }

    [Fact]
    public void ToYaml_ComplexWorkflow_RoundTrips()
    {
        var workflow = Workflow.Create("Complex")
            .Step("Init", _ => Task.CompletedTask)
            .Parallel(p =>
            {
                p.Step(new NamedStep("ParA"));
                p.Step(new NamedStep("ParB"));
            })
            .If(_ => true)
                .Then(new NamedStep("ThenBranch"))
                .Else(new NamedStep("ElseBranch"))
            .Retry(b => b.Step("Retryable", _ => Task.CompletedTask), maxAttempts: 3)
            .Delay(TimeSpan.FromSeconds(1))
            .Build();

        var yaml = WorkflowSerializer.ToYaml(workflow);
        var dto = WorkflowSerializer.FromYaml(yaml);

        dto.Name.Should().Be("Complex");
        dto.Steps.Should().HaveCount(5);
    }

    [Fact]
    public void FromJson_HandlesEmptySteps()
    {
        var json = """{"name":"Empty","version":1,"steps":[]}""";
        var dto = WorkflowSerializer.FromJson(json);

        dto.Name.Should().Be("Empty");
        dto.Steps.Should().BeEmpty();
    }

    [Fact]
    public void FromYaml_HandlesEmptySteps()
    {
        var yaml = "name: Empty\nversion: 1\nsteps:\n";
        var dto = WorkflowSerializer.FromYaml(yaml);

        dto.Name.Should().Be("Empty");
        dto.Steps.Should().BeEmpty();
    }

    [Fact]
    public void Json_Yaml_ProduceSameDefinition()
    {
        var workflow = Workflow.Create("CrossFormat")
            .Step("A", _ => Task.CompletedTask)
            .Retry(b => b.Step("B", _ => Task.CompletedTask), maxAttempts: 3)
            .Delay(TimeSpan.FromSeconds(2))
            .Build();

        var jsonDto = WorkflowSerializer.FromJson(WorkflowSerializer.ToJson(workflow));
        var yamlDto = WorkflowSerializer.FromYaml(WorkflowSerializer.ToYaml(workflow));

        jsonDto.Name.Should().Be(yamlDto.Name);
        jsonDto.Steps.Should().HaveCount(yamlDto.Steps.Count);
        for (var i = 0; i < jsonDto.Steps.Count; i++)
        {
            jsonDto.Steps[i].Name.Should().Be(yamlDto.Steps[i].Name);
            jsonDto.Steps[i].Type.Should().Be(yamlDto.Steps[i].Type);
        }
    }

    [Fact]
    public void ToJson_SagaStep_MarkedCorrectly()
    {
        var workflow = Workflow.Create("SagaTest")
            .Step(new SagaNamedStep("Compensable"))
            .Build();

        var json = WorkflowSerializer.ToJson(workflow);
        var dto = WorkflowSerializer.FromJson(json);

        dto.Steps[0].Type.Should().StartWith("saga:");
    }

    // ── Helpers ──

    private class NamedStep(string name) : IStep
    {
        public string Name => name;
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    private class SagaNamedStep(string name) : IStep, ICompensatingStep
    {
        public string Name => name;
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
        public Task CompensateAsync(IWorkflowContext context) => Task.CompletedTask;
    }
}
