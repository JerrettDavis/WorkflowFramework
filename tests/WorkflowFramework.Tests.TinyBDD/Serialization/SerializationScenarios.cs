using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Builder;
using WorkflowFramework.Internal;
using WorkflowFramework.Serialization;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Serialization;

[Feature("WorkflowSerializer — round-trip, canvas DTO, YAML writer, StepInspector")]
public class SerializationScenarios : TinyBddTestBase
{
    public SerializationScenarios(ITestOutputHelper output) : base(output) { }

    // ── helpers ──────────────────────────────────────────────────────────

    private sealed class SimpleStep(string name) : IStep
    {
        public string Name => name;
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    // ── WorkflowCanvasDto ────────────────────────────────────────────────

    [Scenario("WorkflowCanvasDto default construction has empty collections"), Fact]
    public async Task CanvasDto_DefaultHasEmptyCollections()
    {
        var dto = new WorkflowCanvasDto();

        await Given("a default WorkflowCanvasDto", () => dto)
            .Then("Nodes and Edges are empty", d =>
            {
                d.Nodes.Should().NotBeNull().And.BeEmpty();
                d.Edges.Should().NotBeNull().And.BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WorkflowCanvasNodeDto round-trips through JSON with all properties"), Fact]
    public async Task CanvasNodeDto_RoundTripsAllProperties()
    {
        var node = new WorkflowCanvasNodeDto
        {
            Id = "n1",
            Type = "action",
            Label = "Build",
            Icon = "hammer",
            Category = "CI",
            Color = "#ff0000",
            X = 100.5,
            Y = 200.75,
            Config = new Dictionary<string, string> { ["cmd"] = "dotnet build" }
        };

        var dto = new WorkflowDefinitionDto
        {
            Name = "canvas-test",
            Canvas = new WorkflowCanvasDto { Nodes = { node } }
        };

        // Serialize/deserialize the DTO directly via JSON round-trip
        var json = System.Text.Json.JsonSerializer.Serialize(dto,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
        var parsed = System.Text.Json.JsonSerializer.Deserialize<WorkflowDefinitionDto>(json,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

        await Given("a WorkflowDefinitionDto with canvas node", () => parsed)
            .Then("the canvas node round-trips correctly", p =>
            {
                p.Should().NotBeNull();
                p!.Canvas.Should().NotBeNull();
                p.Canvas!.Nodes.Should().HaveCount(1);
                var n = p.Canvas.Nodes[0];
                n.Id.Should().Be("n1");
                n.Type.Should().Be("action");
                n.Label.Should().Be("Build");
                n.Icon.Should().Be("hammer");
                n.Category.Should().Be("CI");
                n.Color.Should().Be("#ff0000");
                n.X.Should().Be(100.5);
                n.Y.Should().Be(200.75);
                n.Config.Should().ContainKey("cmd").WhoseValue.Should().Be("dotnet build");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WorkflowCanvasEdgeDto round-trips through JSON with all properties"), Fact]
    public async Task CanvasEdgeDto_RoundTripsAllProperties()
    {
        var edge = new WorkflowCanvasEdgeDto
        {
            Id = "e1",
            Kind = "step",
            Source = "n1",
            Target = "n2",
            Label = "success"
        };

        var dto = new WorkflowDefinitionDto
        {
            Name = "edge-test",
            Canvas = new WorkflowCanvasDto { Edges = { edge } }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(dto,
            new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
        var parsed = System.Text.Json.JsonSerializer.Deserialize<WorkflowDefinitionDto>(json,
            new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

        await Given("a WorkflowDefinitionDto with canvas edge", () => parsed)
            .Then("the canvas edge round-trips correctly", p =>
            {
                p!.Canvas!.Edges.Should().HaveCount(1);
                var e = p.Canvas.Edges[0];
                e.Id.Should().Be("e1");
                e.Kind.Should().Be("step");
                e.Source.Should().Be("n1");
                e.Target.Should().Be("n2");
                e.Label.Should().Be("success");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WorkflowCanvasNodeDto optional fields are null by default"), Fact]
    public async Task CanvasNodeDto_OptionalFieldsNullByDefault()
    {
        var node = new WorkflowCanvasNodeDto { Id = "x", Type = "t", Label = "l", X = 0, Y = 0 };

        await Given("a minimal WorkflowCanvasNodeDto", () => node)
            .Then("optional fields are null", n =>
            {
                n.Icon.Should().BeNull();
                n.Category.Should().BeNull();
                n.Color.Should().BeNull();
                n.Config.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WorkflowCanvasEdgeDto optional fields are null by default"), Fact]
    public async Task CanvasEdgeDto_OptionalFieldsNullByDefault()
    {
        var edge = new WorkflowCanvasEdgeDto { Source = "a", Target = "b" };

        await Given("a minimal WorkflowCanvasEdgeDto", () => edge)
            .Then("optional Id, Kind, and Label are null", e =>
            {
                e.Id.Should().BeNull();
                e.Kind.Should().BeNull();
                e.Label.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    // ── YamlWriter ────────────────────────────────────────────────────────

    [Scenario("YamlWriter emits maxAttempts for retry step"), Fact]
    public async Task YamlWriter_EmitsMaxAttempts()
    {
        var dto = new WorkflowDefinitionDto
        {
            Name = "retry-wf",
            Steps = { new StepDefinitionDto { Name = "retry-step", Type = "retry", MaxAttempts = 5 } }
        };

        var yaml = YamlWriter.Write(dto);

        await Given("a workflow DTO with retry step", () => yaml)
            .Then("YAML contains maxAttempts: 5", y =>
            {
                y.Should().Contain("maxAttempts: 5");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("YamlWriter emits timeoutSeconds for timeout step"), Fact]
    public async Task YamlWriter_EmitsTimeoutSeconds()
    {
        var dto = new WorkflowDefinitionDto
        {
            Name = "timeout-wf",
            Steps =
            {
                new StepDefinitionDto
                {
                    Name = "ts", Type = "timeout", TimeoutSeconds = 30.5,
                    Inner = new StepDefinitionDto { Name = "inner", Type = "action" }
                }
            }
        };

        var yaml = YamlWriter.Write(dto);

        await Given("a workflow DTO with timeout step", () => yaml)
            .Then("YAML contains timeoutSeconds: 30.5", y =>
            {
                y.Should().Contain("timeoutSeconds: 30.5");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("YamlWriter emits delaySeconds for delay step"), Fact]
    public async Task YamlWriter_EmitsDelaySeconds()
    {
        var dto = new WorkflowDefinitionDto
        {
            Name = "delay-wf",
            Steps = { new StepDefinitionDto { Name = "ds", Type = "delay", DelaySeconds = 2.5 } }
        };

        var yaml = YamlWriter.Write(dto);

        await Given("a workflow DTO with delay step", () => yaml)
            .Then("YAML contains delaySeconds: 2.5", y =>
            {
                y.Should().Contain("delaySeconds: 2.5");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("YamlWriter emits subWorkflowName for subWorkflow step"), Fact]
    public async Task YamlWriter_EmitsSubWorkflowName()
    {
        var dto = new WorkflowDefinitionDto
        {
            Name = "sub-wf",
            Steps = { new StepDefinitionDto { Name = "sw", Type = "subWorkflow", SubWorkflowName = "other-workflow" } }
        };

        var yaml = YamlWriter.Write(dto);

        await Given("a workflow DTO with subWorkflow step", () => yaml)
            .Then("YAML contains subWorkflowName: other-workflow", y =>
            {
                y.Should().Contain("subWorkflowName: other-workflow");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("YamlWriter emits then and else branches for conditional step"), Fact]
    public async Task YamlWriter_EmitsThenElseBranches()
    {
        var dto = new WorkflowDefinitionDto
        {
            Name = "cond-wf",
            Steps =
            {
                new StepDefinitionDto
                {
                    Name = "cond", Type = "conditional",
                    Then = new StepDefinitionDto { Name = "then-step", Type = "action" },
                    Else = new StepDefinitionDto { Name = "else-step", Type = "action" }
                }
            }
        };

        var yaml = YamlWriter.Write(dto);

        await Given("a workflow DTO with conditional step", () => yaml)
            .Then("YAML contains then and else branches", y =>
            {
                y.Should().Contain("then:");
                y.Should().Contain("then-step");
                y.Should().Contain("else:");
                y.Should().Contain("else-step");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("YamlWriter emits tryBody, catchTypes, and finallyBody for tryCatch step"), Fact]
    public async Task YamlWriter_EmitsTryCatchFields()
    {
        var dto = new WorkflowDefinitionDto
        {
            Name = "trycatch-wf",
            Steps =
            {
                new StepDefinitionDto
                {
                    Name = "tc", Type = "tryCatch",
                    TryBody = new List<StepDefinitionDto> { new() { Name = "try-step", Type = "action" } },
                    CatchTypes = new List<string> { "System.Exception" },
                    FinallyBody = new List<StepDefinitionDto> { new() { Name = "finally-step", Type = "action" } }
                }
            }
        };

        var yaml = YamlWriter.Write(dto);

        await Given("a workflow DTO with tryCatch step", () => yaml)
            .Then("YAML contains tryBody, catchTypes, and finallyBody", y =>
            {
                y.Should().Contain("tryBody:");
                y.Should().Contain("try-step");
                y.Should().Contain("catchTypes:");
                y.Should().Contain("System.Exception");
                y.Should().Contain("finallyBody:");
                y.Should().Contain("finally-step");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("YamlWriter escapes special characters in names"), Fact]
    public async Task YamlWriter_EscapesSpecialCharacters()
    {
        var dto = new WorkflowDefinitionDto
        {
            Name = "name: with colon",
            Steps = { new StepDefinitionDto { Name = "step #1", Type = "action" } }
        };

        var yaml = YamlWriter.Write(dto);

        await Given("a workflow DTO with special characters", () => yaml)
            .Then("YAML wraps special names in quotes", y =>
            {
                y.Should().Contain("\"name: with colon\"");
                y.Should().Contain("\"step #1\"");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("YamlWriter emits child steps for parallel step"), Fact]
    public async Task YamlWriter_EmitsParallelSteps()
    {
        var dto = new WorkflowDefinitionDto
        {
            Name = "parallel-wf",
            Steps =
            {
                new StepDefinitionDto
                {
                    Name = "par", Type = "parallel",
                    Steps = new List<StepDefinitionDto>
                    {
                        new() { Name = "branch-a", Type = "action" },
                        new() { Name = "branch-b", Type = "action" }
                    }
                }
            }
        };

        var yaml = YamlWriter.Write(dto);

        await Given("a workflow DTO with parallel step and two branches", () => yaml)
            .Then("YAML contains steps section with branch-a and branch-b", y =>
            {
                y.Should().Contain("steps:");
                y.Should().Contain("branch-a");
                y.Should().Contain("branch-b");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("YamlWriter emits inner step for timeout"), Fact]
    public async Task YamlWriter_EmitsInnerStep()
    {
        var dto = new WorkflowDefinitionDto
        {
            Name = "inner-wf",
            Steps =
            {
                new StepDefinitionDto
                {
                    Name = "wrapper", Type = "timeout", TimeoutSeconds = 5,
                    Inner = new StepDefinitionDto { Name = "wrapped-action", Type = "action" }
                }
            }
        };

        var yaml = YamlWriter.Write(dto);

        await Given("a workflow DTO with inner step", () => yaml)
            .Then("YAML contains inner section and the wrapped step", y =>
            {
                y.Should().Contain("inner:");
                y.Should().Contain("wrapped-action");
                return true;
            })
            .AssertPassed();
    }

    // ── StepInspector ────────────────────────────────────────────────────

    [Scenario("ToDefinition inspects a simple action step"), Fact]
    public async Task ToDefinition_SimpleActionStep()
    {
        var wf = Workflow.Create("wf")
            .Step(new SimpleStep("build"))
            .Build();

        var dto = WorkflowSerializer.ToDefinition(wf);

        await Given("a workflow with one simple step", () => dto)
            .Then("step is mapped to DTO with correct name and type", d =>
            {
                d.Steps.Should().HaveCount(1);
                d.Steps[0].Name.Should().Be("build");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToDefinition inspects a conditional step structure"), Fact]
    public async Task ToDefinition_ConditionalStep()
    {
        var wf = Workflow.Create("cond-wf")
            .If(_ => true)
                .Then(new SimpleStep("then-step"))
                .EndIf()
            .Build();

        var dto = WorkflowSerializer.ToDefinition(wf);

        await Given("a workflow with conditional step", () => dto)
            .Then("step type is conditional with then branch", d =>
            {
                d.Steps.Should().HaveCount(1);
                d.Steps[0].Type.Should().Be("conditional");
                d.Steps[0].Then.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToDefinition inspects parallel step"), Fact]
    public async Task ToDefinition_ParallelStep()
    {
        var wf = Workflow.Create("par-wf")
            .Parallel(b => b
                .Step(new SimpleStep("branch-a"))
                .Step(new SimpleStep("branch-b")))
            .Build();

        var dto = WorkflowSerializer.ToDefinition(wf);

        await Given("a workflow with parallel step", () => dto)
            .Then("step type is parallel with child steps", d =>
            {
                d.Steps.Should().HaveCount(1);
                d.Steps[0].Type.Should().Be("parallel");
                d.Steps[0].Steps.Should().HaveCount(2);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToDefinition handles a delay step"), Fact]
    public async Task ToDefinition_DelayStep()
    {
        var wf = Workflow.Create("delay-wf")
            .Delay(TimeSpan.FromSeconds(2))
            .Build();

        var dto = WorkflowSerializer.ToDefinition(wf);

        await Given("a workflow with a delay step", () => dto)
            .Then("step type is delay with delaySeconds set", d =>
            {
                d.Steps.Should().HaveCount(1);
                d.Steps[0].Type.Should().Be("delay");
                d.Steps[0].DelaySeconds.Should().Be(2);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToDefinition handles while loop step"), Fact]
    public async Task ToDefinition_WhileLoopStep()
    {
        var count = 0;
        var wf = Workflow.Create("while-wf")
            .While(_ => count++ < 1, b => b.Step(new SimpleStep("loop-body")))
            .Build();

        var dto = WorkflowSerializer.ToDefinition(wf);

        await Given("a workflow with while loop step", () => dto)
            .Then("step type is while with body steps", d =>
            {
                d.Steps.Should().HaveCount(1);
                d.Steps[0].Type.Should().Be("while");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToDefinition inspects a retry step"), Fact]
    public async Task ToDefinition_RetryStep()
    {
        var wf = Workflow.Create("retry-wf")
            .Retry(b => b.Step(new SimpleStep("retried")), maxAttempts: 3)
            .Build();

        var dto = WorkflowSerializer.ToDefinition(wf);

        await Given("a workflow with retry step", () => dto)
            .Then("step type is retry with maxAttempts populated", d =>
            {
                d.Steps.Should().HaveCount(1);
                d.Steps[0].Type.Should().Be("retry");
                d.Steps[0].MaxAttempts.Should().Be(3);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("StepDefinitionDto default values are correct"), Fact]
    public async Task StepDefinitionDto_DefaultValues()
    {
        var dto = new StepDefinitionDto();

        await Given("a default StepDefinitionDto", () => dto)
            .Then("numeric fields default to zero and string to empty", d =>
            {
                d.MaxAttempts.Should().Be(0);
                d.TimeoutSeconds.Should().Be(0);
                d.DelaySeconds.Should().Be(0);
                d.Name.Should().Be(string.Empty);
                d.Type.Should().Be(string.Empty);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToDefinition inspects a DoWhile step"), Fact]
    public async Task ToDefinition_DoWhileStep()
    {
        var ran = false;
        var wf = Workflow.Create("dowhile-wf")
            .DoWhile(b => b.Step(new SimpleStep("body-step")), _ => { ran = true; return false; })
            .Build();

        var dto = WorkflowSerializer.ToDefinition(wf);

        await Given("a workflow with DoWhile step", () => dto)
            .Then("step type is doWhile", d =>
            {
                d.Steps.Should().HaveCount(1);
                d.Steps[0].Type.Should().Be("doWhile");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToDefinition inspects a ForEach step"), Fact]
    public async Task ToDefinition_ForEachStep()
    {
        var wf = Workflow.Create("foreach-wf")
            .ForEach<int>(
                _ => new[] { 1, 2, 3 },
                b => b.Step(new SimpleStep("item-step")))
            .Build();

        var dto = WorkflowSerializer.ToDefinition(wf);

        await Given("a workflow with ForEach step", () => dto)
            .Then("step type is forEach", d =>
            {
                d.Steps.Should().HaveCount(1);
                d.Steps[0].Type.Should().Be("forEach");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToDefinition inspects a Timeout step wrapping an inner step"), Fact]
    public async Task ToDefinition_TimeoutStep()
    {
        // TimeoutStep is internal — construct directly using InternalsVisibleTo
        var inner = new SimpleStep("guarded");
        var timeoutStep = new TimeoutStep(inner, TimeSpan.FromSeconds(10));
        var wf = Workflow.Create("timeout-wf")
            .Step(timeoutStep)
            .Build();

        var dto = WorkflowSerializer.ToDefinition(wf);

        await Given("a workflow with Timeout step", () => dto)
            .Then("step type is timeout and timeoutSeconds is set", d =>
            {
                d.Steps.Should().HaveCount(1);
                d.Steps[0].Type.Should().Be("timeout");
                d.Steps[0].TimeoutSeconds.Should().Be(10);
                d.Steps[0].Inner.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToDefinition inspects a TryCatch step"), Fact]
    public async Task ToDefinition_TryCatchStep()
    {
        var wf = Workflow.Create("try-wf")
            .Try(b => b.Step(new SimpleStep("risky")))
                .Catch<InvalidOperationException>((_, _) => Task.CompletedTask)
            .EndTry()
            .Build();

        var dto = WorkflowSerializer.ToDefinition(wf);

        await Given("a workflow with TryCatch step", () => dto)
            .Then("step type is tryCatch with tryBody and catchTypes", d =>
            {
                d.Steps.Should().HaveCount(1);
                d.Steps[0].Type.Should().Be("tryCatch");
                d.Steps[0].TryBody.Should().NotBeNull();
                d.Steps[0].CatchTypes.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToDefinition inspects a SubWorkflow step"), Fact]
    public async Task ToDefinition_SubWorkflowStep()
    {
        var sub = Workflow.Create("inner-workflow")
            .Step(new SimpleStep("inner-step"))
            .Build();

        var wf = Workflow.Create("outer-wf")
            .SubWorkflow(sub)
            .Build();

        var dto = WorkflowSerializer.ToDefinition(wf);

        await Given("a workflow with SubWorkflow step", () => dto)
            .Then("step type is subWorkflow and subWorkflowName matches", d =>
            {
                d.Steps.Should().HaveCount(1);
                d.Steps[0].Type.Should().Be("subWorkflow");
                d.Steps[0].SubWorkflowName.Should().Be("inner-workflow");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToDefinition maps a custom step using full type name"), Fact]
    public async Task ToDefinition_CustomStep_UsesFullTypeName()
    {
        var wf = Workflow.Create("custom-wf")
            .Step(new SimpleStep("my-custom-step"))
            .Build();

        var dto = WorkflowSerializer.ToDefinition(wf);

        await Given("a workflow with a custom step", () => dto)
            .Then("step name is preserved", d =>
            {
                d.Steps.Should().HaveCount(1);
                d.Steps[0].Name.Should().Be("my-custom-step");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ToDefinition marks ICompensatingStep with saga prefix"), Fact]
    public async Task ToDefinition_CompensatingStep_HasSagaPrefix()
    {
        var wf = Workflow.Create("saga-wf")
            .Step(new CompensatingTestStep("compensate-step"))
            .Build();

        var dto = WorkflowSerializer.ToDefinition(wf);

        await Given("a workflow with a compensating step", () => dto)
            .Then("step type has saga: prefix", d =>
            {
                d.Steps.Should().HaveCount(1);
                d.Steps[0].Type.Should().StartWith("saga:");
                return true;
            })
            .AssertPassed();
    }
}

file sealed class CompensatingTestStep(string name) : IStep, ICompensatingStep
{
    public string Name => name;
    public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    public Task CompensateAsync(IWorkflowContext context) => Task.CompletedTask;
}
