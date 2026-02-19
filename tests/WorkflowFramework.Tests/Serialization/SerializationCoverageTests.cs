using FluentAssertions;
using WorkflowFramework.Serialization;
using Xunit;

namespace WorkflowFramework.Tests.Serialization;

/// <summary>
/// Coverage tests for YAML reader/writer edge cases via the public WorkflowSerializer API.
/// </summary>
public class SerializationCoverageTests
{
    [Fact]
    public void FromYaml_AllStepTypes()
    {
        var yaml = @"name: full-test
version: 3
steps:
  - name: retry-step
    type: retry
    maxAttempts: 5
    inner:
      - name: inner-action
        type: action
  - name: timeout-step
    type: timeout
    timeoutSeconds: 30.5
    inner:
      - name: inner-timeout
        type: action
  - name: delay-step
    type: delay
    delaySeconds: 2.5
  - name: sub-wf
    type: subWorkflow
    subWorkflowName: other-workflow
  - name: cond-step
    type: conditional
    then:
      - name: then-action
        type: action
    else:
      - name: else-action
        type: action
  - name: parallel-step
    type: parallel
    steps:
      - name: branch-a
        type: action
      - name: branch-b
        type: action
  - name: try-step
    type: tryCatch
    tryBody:
      - name: try-action
        type: action
    catchTypes:
      - System.Exception
      - System.IO.IOException
    finallyBody:
      - name: finally-action
        type: action";

        var parsed = WorkflowSerializer.FromYaml(yaml);

        parsed.Name.Should().Be("full-test");
        parsed.Version.Should().Be(3);
        parsed.Steps.Should().HaveCount(7);

        parsed.Steps[0].MaxAttempts.Should().Be(5);
        parsed.Steps[0].Inner.Should().NotBeNull();
        parsed.Steps[0].Inner!.Name.Should().Be("inner-action");

        parsed.Steps[1].TimeoutSeconds.Should().Be(30.5);
        parsed.Steps[2].DelaySeconds.Should().Be(2.5);
        parsed.Steps[3].SubWorkflowName.Should().Be("other-workflow");

        parsed.Steps[4].Then.Should().NotBeNull();
        parsed.Steps[4].Else.Should().NotBeNull();

        parsed.Steps[5].Steps.Should().HaveCount(2);

        parsed.Steps[6].TryBody.Should().HaveCount(1);
        parsed.Steps[6].CatchTypes.Should().HaveCount(2);
        parsed.Steps[6].FinallyBody.Should().HaveCount(1);
    }

    [Fact]
    public void FromYaml_SpecialCharactersInQuotes()
    {
        var yaml = @"name: ""name: with colon""
version: 1
steps:
  - name: ""step #1""
    type: action
  - name: ""step \""quoted\""""
    type: action
  - name: """"
    type: action";

        var parsed = WorkflowSerializer.FromYaml(yaml);
        parsed.Name.Should().Be("name: with colon");
        parsed.Steps[0].Name.Should().Be("step #1");
        parsed.Steps[1].Name.Should().Be("step \"quoted\"");
        parsed.Steps[2].Name.Should().Be("");
    }

    [Fact]
    public void FromYaml_EmptyWorkflow()
    {
        var yaml = @"name: empty
version: 1
steps:";

        var parsed = WorkflowSerializer.FromYaml(yaml);
        parsed.Name.Should().Be("empty");
        parsed.Steps.Should().BeEmpty();
    }

    [Fact]
    public void FromYaml_IgnoresCommentLines()
    {
        var yaml = @"# This is a comment
name: test
version: 1
# Another comment
steps:
  - name: s1
    type: action";
        var parsed = WorkflowSerializer.FromYaml(yaml);
        parsed.Name.Should().Be("test");
        parsed.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void FromYaml_UnknownTopLevelKey_Ignored()
    {
        var yaml = @"name: test
unknown_key: whatever
version: 2
steps:
  - name: s1
    type: action";
        var parsed = WorkflowSerializer.FromYaml(yaml);
        parsed.Name.Should().Be("test");
        parsed.Version.Should().Be(2);
    }

    [Fact]
    public void FromYaml_NonNumericVersion_DefaultsTo1()
    {
        var yaml = @"name: test
version: abc
steps:
  - name: s1
    type: action";
        var parsed = WorkflowSerializer.FromYaml(yaml);
        parsed.Version.Should().Be(1);
    }

    [Fact]
    public void FromJson_TryCatch_AllFields()
    {
        var json = @"{
            ""name"": ""json-test"",
            ""version"": 2,
            ""steps"": [{
                ""name"": ""tryCatch"",
                ""type"": ""tryCatch"",
                ""tryBody"": [{ ""name"": ""try1"", ""type"": ""action"" }],
                ""catchTypes"": [""Exception""],
                ""finallyBody"": [{ ""name"": ""fin1"", ""type"": ""action"" }]
            }]
        }";

        var parsed = WorkflowSerializer.FromJson(json);
        parsed.Steps[0].TryBody.Should().HaveCount(1);
        parsed.Steps[0].CatchTypes.Should().Contain("Exception");
        parsed.Steps[0].FinallyBody.Should().HaveCount(1);
    }

    [Fact]
    public void ToDefinition_SimpleWorkflow()
    {
        var wf = Workflow.Create("wf1")
            .Step(new TestStep("s1"))
            .Step(new TestStep("s2"))
            .Build();

        var dto = WorkflowSerializer.ToDefinition(wf);
        dto.Name.Should().Be("wf1");
        dto.Steps.Should().HaveCount(2);
    }

    [Fact]
    public void ToJson_FromWorkflow_ContainsName()
    {
        var wf = Workflow.Create("json-wf")
            .Step(new TestStep("s1"))
            .Build();

        var json = WorkflowSerializer.ToJson(wf);
        json.Should().Contain("json-wf");
    }

    [Fact]
    public void ToYaml_FromWorkflow_ContainsName()
    {
        var wf = Workflow.Create("yaml-wf")
            .Step(new TestStep("s1"))
            .Build();

        var yaml = WorkflowSerializer.ToYaml(wf);
        yaml.Should().Contain("yaml-wf");
    }

    [Fact]
    public void FromYaml_NoColonLine_ParsedAsKey()
    {
        // Edge case: line with no colon
        var yaml = @"name: test
version: 1
steps:
  - name: s1
    type: action";
        var parsed = WorkflowSerializer.FromYaml(yaml);
        parsed.Name.Should().Be("test");
    }

    private class TestStep : IStep
    {
        public TestStep(string name) => Name = name;
        public string Name { get; }
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }
}
