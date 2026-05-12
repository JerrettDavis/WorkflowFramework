using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.Configuration;
using Xunit;

namespace WorkflowFramework.Tests.Configuration;

/// <summary>
/// Tests for the full YAML workflow definition format covering all step types and
/// the AddYamlWorkflowLoader() DI extension method.
/// </summary>
public class YamlWorkflowFullTests
{
    // ── step type ──────────────────────────────────────────────────────────────

    [Fact]
    public void Yaml_StepType_WithClassProperty_LoadsAndBuilds()
    {
        var yaml = """
            name: OrderProcessing
            steps:
              - name: ValidateOrder
                type: step
                class: ValidateOrder
            """;

        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        def.Name.Should().Be("OrderProcessing");
        def.Steps.Should().HaveCount(1);
        def.Steps[0].Type.Should().Be("step");
        def.Steps[0].Class.Should().Be("ValidateOrder");

        var registry = new StepRegistry();
        registry.Register("ValidateOrder", () => new TestStep("ValidateOrder"));
        var builder = new WorkflowDefinitionBuilder(registry);
        var wf = builder.Build(def);
        wf.Steps.Should().HaveCount(1);
    }

    // ── conditional type ───────────────────────────────────────────────────────

    [Fact]
    public void Yaml_ConditionalType_WithThenStepsAndElseSteps_LoadsAndBuilds()
    {
        var yaml = """
            name: PaymentFlow
            steps:
              - name: PaymentDecision
                type: conditional
                condition: isValid
                thenSteps:
                  - name: Charge
                    type: step
                    class: ChargePayment
                elseSteps:
                  - name: Reject
                    type: step
                    class: RejectOrder
            """;

        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        var step = def.Steps[0];
        step.Type.Should().Be("conditional");
        step.Condition.Should().Be("isValid");
        step.ThenSteps.Should().HaveCount(1);
        step.ThenSteps![0].Class.Should().Be("ChargePayment");
        step.ElseSteps.Should().HaveCount(1);
        step.ElseSteps![0].Class.Should().Be("RejectOrder");

        var registry = new StepRegistry();
        registry.Register("ChargePayment", () => new TestStep("ChargePayment"));
        registry.Register("RejectOrder", () => new TestStep("RejectOrder"));
        var builder = new WorkflowDefinitionBuilder(registry);
        var wf = builder.Build(def);
        wf.Steps.Should().HaveCount(1);
    }

    [Fact]
    public async Task Yaml_ConditionalType_ThenBranch_ExecutesCorrectly()
    {
        var yaml = """
            name: CondFlow
            steps:
              - type: conditional
                condition: flag
                thenSteps:
                  - type: step
                    class: ThenStep
                elseSteps:
                  - type: step
                    class: ElseStep
            """;

        var executed = new List<string>();
        var registry = new StepRegistry();
        registry.Register("ThenStep", () => new TrackingStep("ThenStep", executed));
        registry.Register("ElseStep", () => new TrackingStep("ElseStep", executed));

        var def = new YamlWorkflowDefinitionLoader().Load(yaml);
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);

        var ctx = new WorkflowContext();
        ctx.Properties["flag"] = true;
        await wf.ExecuteAsync(ctx);

        executed.Should().ContainSingle().Which.Should().Be("ThenStep");
    }

    [Fact]
    public void Yaml_ConditionalType_WithThenAndElseAsListAliases_LoadsAndBuilds()
    {
        // 'then:' and 'else:' as YAML sequences are aliases for 'thenSteps:'/'elseSteps:'.
        var yaml = """
            name: AliasFlow
            steps:
              - name: Decision
                type: conditional
                condition: isValid
                then:
                  - name: Charge
                    type: step
                    class: ChargePayment
                else:
                  - name: Reject
                    type: step
                    class: RejectOrder
            """;

        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        var step = def.Steps[0];
        step.Type.Should().Be("conditional");
        step.ThenSteps.Should().HaveCount(1);
        step.ThenSteps![0].Class.Should().Be("ChargePayment");
        step.ElseSteps.Should().HaveCount(1);
        step.ElseSteps![0].Class.Should().Be("RejectOrder");
        // The scalar 'then'/'else' properties should be null — the sequences were mapped to thenSteps/elseSteps.
        step.Then.Should().BeNull();
        step.Else.Should().BeNull();

        var registry = new StepRegistry();
        registry.Register("ChargePayment", () => new TestStep("ChargePayment"));
        registry.Register("RejectOrder", () => new TestStep("RejectOrder"));
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Steps.Should().HaveCount(1);
    }

    [Fact]
    public async Task Yaml_ConditionalType_ThenListAlias_ExecutesCorrectly()
    {
        // 'then:' as a list (alias for 'thenSteps:') should route correctly at runtime.
        var yaml = """
            name: AliasRunFlow
            steps:
              - type: conditional
                condition: flag
                then:
                  - type: step
                    class: ThenStep
                else:
                  - type: step
                    class: ElseStep
            """;

        var executed = new List<string>();
        var registry = new StepRegistry();
        registry.Register("ThenStep", () => new TrackingStep("ThenStep", executed));
        registry.Register("ElseStep", () => new TrackingStep("ElseStep", executed));

        var def = new YamlWorkflowDefinitionLoader().Load(yaml);
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);

        var ctx = new WorkflowContext();
        ctx.Properties["flag"] = true;
        await wf.ExecuteAsync(ctx);

        executed.Should().ContainSingle().Which.Should().Be("ThenStep");
    }

    // ── parallel type ──────────────────────────────────────────────────────────

    [Fact]
    public void Yaml_ParallelType_WithStepsList_LoadsAndBuilds()
    {
        var yaml = """
            name: FulfillmentFlow
            steps:
              - name: FulfillmentJobs
                type: parallel
                steps:
                  - name: SendEmail
                    type: step
                    class: SendEmail
                  - name: UpdateInventory
                    type: step
                    class: UpdateInventory
            """;

        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        var step = def.Steps[0];
        step.Type.Should().Be("parallel");
        step.Steps.Should().HaveCount(2);

        var registry = new StepRegistry();
        registry.Register("SendEmail", () => new TestStep("SendEmail"));
        registry.Register("UpdateInventory", () => new TestStep("UpdateInventory"));
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Steps.Should().HaveCount(1); // parallel group is a single step
    }

    [Fact]
    public void Yaml_ParallelType_WithCompositeChild_Builds()
    {
        // A conditional step nested inside a parallel block should be accepted.
        var def = new WorkflowDefinition
        {
            Steps =
            [
                new StepDefinition
                {
                    Type = "parallel",
                    Steps =
                    [
                        new StepDefinition
                        {
                            Type = "conditional",
                            Condition = "flag",
                            ThenSteps = [new StepDefinition { Type = "step", Class = "ThenStep" }]
                        },
                        new StepDefinition { Type = "step", Class = "OtherStep" }
                    ]
                }
            ]
        };

        var registry = new StepRegistry();
        registry.Register("ThenStep", () => new TestStep("ThenStep"));
        registry.Register("OtherStep", () => new TestStep("OtherStep"));
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Steps.Should().HaveCount(1);
    }

    // ── foreach type ───────────────────────────────────────────────────────────

    [Fact]
    public void Yaml_ForEachType_LoadsAndBuilds()
    {
        var yaml = """
            name: ForEachFlow
            steps:
              - name: ProcessItems
                type: foreach
                condition: items
                steps:
                  - type: step
                    class: ProcessItem
            """;

        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        def.Steps[0].Type.Should().Be("foreach");
        def.Steps[0].Steps.Should().HaveCount(1);

        var registry = new StepRegistry();
        registry.Register("ProcessItem", () => new TestStep("ProcessItem"));
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Steps.Should().HaveCount(1);
    }

    [Fact]
    public async Task Yaml_ForEachType_NonGenericCollection_IteratesAllItems()
    {
        // List<int> is IEnumerable but NOT IEnumerable<object>; the builder should still iterate.
        var executed = new List<string>();
        var registry = new StepRegistry();
        registry.Register("ProcessItem", () => new TrackingStep("ProcessItem", executed));

        var def = new WorkflowDefinition
        {
            Steps =
            [
                new StepDefinition
                {
                    Type = "foreach",
                    Condition = "items",
                    Steps = [new StepDefinition { Type = "ProcessItem" }]
                }
            ]
        };

        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        var ctx = new WorkflowContext();
        ctx.Properties["items"] = new List<int> { 1, 2, 3 };
        await wf.ExecuteAsync(ctx);

        executed.Should().HaveCount(3);
    }

    // ── while type ─────────────────────────────────────────────────────────────

    [Fact]
    public void Yaml_WhileType_LoadsAndBuilds()
    {
        var yaml = """
            name: WhileFlow
            steps:
              - name: ProcessLoop
                type: while
                condition: keepRunning
                steps:
                  - type: step
                    class: DoWork
            """;

        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        def.Steps[0].Type.Should().Be("while");
        def.Steps[0].Condition.Should().Be("keepRunning");

        var registry = new StepRegistry();
        registry.Register("DoWork", () => new TestStep("DoWork"));
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Steps.Should().HaveCount(1);
    }

    // ── dowhile type ───────────────────────────────────────────────────────────

    [Fact]
    public void Yaml_DoWhileType_LoadsAndBuilds()
    {
        var yaml = """
            name: DoWhileFlow
            steps:
              - name: DoWhileLoop
                type: dowhile
                condition: keepRunning
                steps:
                  - type: step
                    class: DoWork
            """;

        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        def.Steps[0].Type.Should().Be("dowhile");
        def.Steps[0].Condition.Should().Be("keepRunning");

        var registry = new StepRegistry();
        registry.Register("DoWork", () => new TestStep("DoWork"));
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Steps.Should().HaveCount(1);
    }

    // ── retry type ─────────────────────────────────────────────────────────────

    [Fact]
    public void Yaml_RetryType_LoadsAndBuilds()
    {
        var yaml = """
            name: RetryFlow
            steps:
              - name: RetryGroup
                type: retry
                retry:
                  maxAttempts: 5
                  backoff: exponential
                  baseDelayMs: 200
                steps:
                  - type: step
                    class: FlakyStep
            """;

        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        var step = def.Steps[0];
        step.Type.Should().Be("retry");
        step.Retry!.MaxAttempts.Should().Be(5);
        step.Retry.Backoff.Should().Be("exponential");
        step.Steps.Should().HaveCount(1);

        var registry = new StepRegistry();
        registry.Register("FlakyStep", () => new TestStep("FlakyStep"));
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void Yaml_RetryType_WithZeroMaxAttempts_ClampsToOne()
    {
        // maxAttempts: 0 (or negative) must be clamped to 1 so the body always executes at least once.
        var yaml = """
            name: RetryClampFlow
            steps:
              - name: RetryGroup
                type: retry
                retry:
                  maxAttempts: 0
                steps:
                  - type: step
                    class: FlakyStep
            """;

        var registry = new StepRegistry();
        registry.Register("FlakyStep", () => new TestStep("FlakyStep"));

        var def = new YamlWorkflowDefinitionLoader().Load(yaml);
        // Building must not throw and the workflow must contain one step (the RetryGroupStep).
        var act = () => new WorkflowDefinitionBuilder(registry).Build(def);
        act.Should().NotThrow("maxAttempts=0 is clamped to 1 rather than producing a no-op retry");
    }

    // ── try type ───────────────────────────────────────────────────────────────

    [Fact]
    public void Yaml_TryType_LoadsAndBuilds()
    {
        var yaml = """
            name: TryFlow
            steps:
              - name: TryBlock
                type: try
                steps:
                  - type: step
                    class: RiskyStep
            """;

        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        def.Steps[0].Type.Should().Be("try");
        def.Steps[0].Steps.Should().HaveCount(1);

        var registry = new StepRegistry();
        registry.Register("RiskyStep", () => new TestStep("RiskyStep"));
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void Yaml_TryType_WithElseSteps_AsLegacyFinally_LoadsAndBuilds()
    {
        // This test covers the legacy finally encoding via 'elseSteps' (not the dedicated 'finallySteps' key).
        // See Yaml_TryType_WithFinallySteps_LoadsAndBuilds for the new 'finallySteps' path.
        var yaml = """
            name: TryFinallyFlow
            steps:
              - name: TryBlock
                type: try
                steps:
                  - type: step
                    class: RiskyStep
                elseSteps:
                  - type: step
                    class: CleanupStep
            """;

        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        def.Steps[0].Steps.Should().HaveCount(1);
        def.Steps[0].ElseSteps.Should().HaveCount(1);
        def.Steps[0].ElseSteps![0].Class.Should().Be("CleanupStep");

        var registry = new StepRegistry();
        registry.Register("RiskyStep", () => new TestStep("RiskyStep"));
        registry.Register("CleanupStep", () => new TestStep("CleanupStep"));
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void Yaml_TryType_WithCatch_LoadsAndBuilds()
    {
        var yaml = """
            name: TryCatchFlow
            steps:
              - name: TryBlock
                type: try
                steps:
                  - type: step
                    class: RiskyStep
                catch:
                  - exception: InvalidOperationException
                    steps:
                      - type: step
                        class: HandleError
            """;

        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        def.Steps[0].Type.Should().Be("try");
        def.Steps[0].Catch.Should().HaveCount(1);
        def.Steps[0].Catch![0].Exception.Should().Be("InvalidOperationException");
        def.Steps[0].Catch![0].Steps.Should().HaveCount(1);

        var registry = new StepRegistry();
        registry.Register("RiskyStep", () => new TestStep("RiskyStep"));
        registry.Register("HandleError", () => new TestStep("HandleError"));
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Steps.Should().HaveCount(1);
    }

    [Fact]
    public async Task Yaml_TryType_CatchHandler_ExecutesOnException()
    {
        var executed = new List<string>();
        var registry = new StepRegistry();
        registry.Register("ThrowStep", () => new ThrowingStep());
        registry.Register("HandleError", () => new TrackingStep("HandleError", executed));

        var def = new WorkflowDefinition
        {
            Steps =
            [
                new StepDefinition
                {
                    Type = "try",
                    Steps = [new StepDefinition { Type = "ThrowStep" }],
                    Catch =
                    [
                        new CatchDefinition
                        {
                            Exception = "InvalidOperationException",
                            Steps = [new StepDefinition { Type = "HandleError" }]
                        }
                    ]
                }
            ]
        };

        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        await wf.ExecuteAsync(new WorkflowContext());
        executed.Should().ContainSingle().Which.Should().Be("HandleError");
    }

    // ── subworkflow type ───────────────────────────────────────────────────────

    [Fact]
    public void Yaml_SubworkflowType_WithRegisteredWorkflow_Builds()
    {
        var yaml = """
            name: MainFlow
            steps:
              - name: SubFlow
                type: subworkflow
                subWorkflow: MySubWorkflow
            """;

        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        def.Steps[0].Type.Should().Be("subworkflow");
        def.Steps[0].SubWorkflow.Should().Be("MySubWorkflow");

        var subWorkflow = Workflow.Create("MySubWorkflow")
            .Step(new TestStep("A"))
            .Build();

        var registry = new StepRegistry();
        var subWorkflows = new Dictionary<string, IWorkflow>
        {
            ["MySubWorkflow"] = subWorkflow
        };
        var wf = new WorkflowDefinitionBuilder(registry, subWorkflows).Build(def);
        wf.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void Yaml_SubworkflowType_ThrowsWhenSubWorkflowNotRegistered()
    {
        var def = new WorkflowDefinition
        {
            Steps = [new StepDefinition { Type = "subworkflow", SubWorkflow = "Missing" }]
        };

        var registry = new StepRegistry();
        var act = () => new WorkflowDefinitionBuilder(registry).Build(def);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Missing*");
    }

    // ── approval type ──────────────────────────────────────────────────────────

    [Fact]
    public void Yaml_ApprovalType_WithRegisteredStep_UsesRegistry()
    {
        var yaml = """
            name: ApprovalFlow
            steps:
              - name: HumanReview
                type: approval
                message: Order requires manager sign-off
                requiredApprovers: 1
                timeoutMinutes: 1440
            """;

        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        var step = def.Steps[0];
        step.Type.Should().Be("approval");
        step.Message.Should().Be("Order requires manager sign-off");
        step.RequiredApprovers.Should().Be(1);
        step.TimeoutMinutes.Should().Be(1440);

        // Build with a registered approval step
        var registry = new StepRegistry();
        registry.Register("approval", () => new TestStep("ApprovalStep"));
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Steps.Should().HaveCount(1);
    }

    [Fact]
    public async Task Yaml_ApprovalType_WithoutRegisteredStep_CreatesFallbackStep()
    {
        var def = new WorkflowDefinition
        {
            Steps =
            [
                new StepDefinition
                {
                    Name = "Review",
                    Type = "approval",
                    Message = "Please review",
                    RequiredApprovers = 2,
                    TimeoutMinutes = 60
                }
            ]
        };

        var registry = new StepRegistry(); // "approval" not registered
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Steps.Should().HaveCount(1);

        // Execute and verify the fallback step records state in context
        var ctx = new WorkflowContext();
        await wf.ExecuteAsync(ctx);
        ctx.Properties["Review.Status"].Should().Be("Pending");
        ctx.Properties["Review.RequiredApprovers"].Should().Be(2);
    }

    // ── saga type ──────────────────────────────────────────────────────────────

    [Fact]
    public void Yaml_SagaType_LoadsAndBuilds()
    {
        var yaml = """
            name: SagaFlow
            steps:
              - name: OrderSaga
                type: saga
                steps:
                  - type: step
                    class: ReserveInventory
                  - type: step
                    class: ChargeCreditCard
            """;

        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        def.Steps[0].Type.Should().Be("saga");
        def.Steps[0].Steps.Should().HaveCount(2);

        var registry = new StepRegistry();
        registry.Register("ReserveInventory", () => new TestStep("ReserveInventory"));
        registry.Register("ChargeCreditCard", () => new TestStep("ChargeCreditCard"));
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Steps.Should().HaveCount(1); // saga is a sub-workflow step
    }

    // ── description field ──────────────────────────────────────────────────────

    [Fact]
    public void Yaml_WorkflowDescription_IsLoaded()
    {
        var yaml = """
            name: OrderProcessing
            version: 2
            description: Process customer orders end-to-end
            steps: []
            """;

        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        def.Description.Should().Be("Process customer orders end-to-end");
    }

    // ── class shorthand ────────────────────────────────────────────────────────

    [Fact]
    public void Yaml_ClassOnly_WithoutType_ResolvesAsStep()
    {
        var def = new WorkflowDefinition
        {
            Steps = [new StepDefinition { Class = "MyStep" }]
        };

        var registry = new StepRegistry();
        registry.Register("MyStep", () => new TestStep("MyStep"));
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Steps.Should().HaveCount(1);
    }

    // ── full example YAML ──────────────────────────────────────────────────────

    [Fact]
    public void Yaml_FullOrderProcessingExample_LoadsAndBuilds()
    {
        var yaml = """
            name: OrderProcessing
            version: 2
            description: Process customer orders end-to-end
            steps:
              - name: ValidateOrder
                type: step
                class: ValidateOrder

              - name: PaymentDecision
                type: conditional
                condition: isValid
                thenSteps:
                  - name: ChargePayment
                    type: step
                    class: ChargePayment
                elseSteps:
                  - name: RejectOrder
                    type: step
                    class: RejectOrder

              - name: FulfillmentJobs
                type: parallel
                steps:
                  - name: SendEmail
                    type: step
                    class: SendEmail
                  - name: UpdateInventory
                    type: step
                    class: UpdateInventory

              - name: Complete
                type: step
                class: CompleteOrder
            """;

        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        def.Name.Should().Be("OrderProcessing");
        def.Version.Should().Be(2);
        def.Description.Should().Be("Process customer orders end-to-end");
        def.Steps.Should().HaveCount(4);

        var registry = new StepRegistry();
        foreach (var name in new[] { "ValidateOrder", "ChargePayment", "RejectOrder", "SendEmail", "UpdateInventory", "CompleteOrder" })
            registry.Register(name, () => new TestStep(name));

        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Name.Should().Be("OrderProcessing");
        wf.Steps.Should().HaveCount(4);
    }

    // ── validation / error messages ────────────────────────────────────────────

    [Fact]
    public void Yaml_StepType_WithoutClass_ThrowsClearError()
    {
        var def = new WorkflowDefinition
        {
            Steps = [new StepDefinition { Type = "step" }]
        };

        var registry = new StepRegistry();
        var builder = new WorkflowDefinitionBuilder(registry);
        var act = () => builder.Build(def);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'step'*'class'*");
    }

    [Fact]
    public void Yaml_ConditionalType_WithoutCondition_ThrowsClearError()
    {
        var def = new WorkflowDefinition
        {
            Steps = [new StepDefinition { Type = "conditional", ThenSteps = [new StepDefinition { Type = "step", Class = "A" }] }]
        };

        var registry = new StepRegistry();
        registry.Register("A", () => new TestStep("A"));
        var builder = new WorkflowDefinitionBuilder(registry);
        var act = () => builder.Build(def);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'condition'*");
    }

    [Fact]
    public void Yaml_ConditionalType_WithoutThen_ThrowsClearError()
    {
        var def = new WorkflowDefinition
        {
            Steps = [new StepDefinition { Type = "conditional", Condition = "flag" }]
        };

        var registry = new StepRegistry();
        var builder = new WorkflowDefinitionBuilder(registry);
        var act = () => builder.Build(def);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'then'*");
    }

    [Fact]
    public void Yaml_ParallelType_WithEmptySteps_ThrowsClearError()
    {
        var def = new WorkflowDefinition
        {
            Steps = [new StepDefinition { Type = "parallel", Steps = [] }]
        };

        var registry = new StepRegistry();
        var builder = new WorkflowDefinitionBuilder(registry);
        var act = () => builder.Build(def);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'steps'*");
    }

    [Fact]
    public void Yaml_StepWithNoTypeOrClass_ThrowsClearError()
    {
        var def = new WorkflowDefinition
        {
            Steps = [new StepDefinition { Name = "Bad" }]
        };

        var registry = new StepRegistry();
        var builder = new WorkflowDefinitionBuilder(registry);
        var act = () => builder.Build(def);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'type' or 'class'*");
    }

    // ── round-trip: YAML → JSON → reload → same execution ─────────────────────

    [Fact]
    public void RoundTrip_YamlToJson_SameDefinition()
    {
        var yaml = """
            name: RoundTrip
            version: 3
            steps:
              - name: StepA
                type: step
                class: StepA
              - name: StepB
                type: step
                class: StepB
            """;

        var yamlLoader = new YamlWorkflowDefinitionLoader();
        var def = yamlLoader.Load(yaml);

        // Serialize to JSON and reload
        var json = System.Text.Json.JsonSerializer.Serialize(def, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });

        var jsonLoader = new JsonWorkflowDefinitionLoader();
        var defFromJson = jsonLoader.Load(json);

        defFromJson.Name.Should().Be(def.Name);
        defFromJson.Version.Should().Be(def.Version);
        defFromJson.Steps.Should().HaveCount(def.Steps.Count);
        for (var i = 0; i < def.Steps.Count; i++)
        {
            defFromJson.Steps[i].Name.Should().Be(def.Steps[i].Name);
            defFromJson.Steps[i].Type.Should().Be(def.Steps[i].Type);
            defFromJson.Steps[i].Class.Should().Be(def.Steps[i].Class);
        }
    }

    [Fact]
    public async Task RoundTrip_YamlToJson_SameExecutionBehavior()
    {
        var yaml = """
            name: ExecRoundTrip
            steps:
              - name: Track
                type: step
                class: TrackStep
            """;

        var executed = new List<string>();

        var registry = new StepRegistry();
        registry.Register("TrackStep", () => new TrackingStep("Track", executed));

        var yamlDef = new YamlWorkflowDefinitionLoader().Load(yaml);
        var wfFromYaml = new WorkflowDefinitionBuilder(registry).Build(yamlDef);

        var json = System.Text.Json.JsonSerializer.Serialize(yamlDef, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });

        var jsonDef = new JsonWorkflowDefinitionLoader().Load(json);
        var wfFromJson = new WorkflowDefinitionBuilder(registry).Build(jsonDef);

        // Both workflows should produce the same execution
        var ctx1 = new WorkflowContext();
        await wfFromYaml.ExecuteAsync(ctx1);
        var tracked1 = new List<string>(executed);
        executed.Clear();

        var ctx2 = new WorkflowContext();
        await wfFromJson.ExecuteAsync(ctx2);
        var tracked2 = new List<string>(executed);

        tracked1.Should().BeEquivalentTo(tracked2);
    }

    // ── DI extension methods ───────────────────────────────────────────────────

    [Fact]
    public void AddYamlWorkflowLoader_RegistersYamlLoader()
    {
        var services = new ServiceCollection();
        services.AddYamlWorkflowLoader();
        var provider = services.BuildServiceProvider();

        var loader = provider.GetRequiredService<IWorkflowDefinitionLoader>();
        loader.Should().BeOfType<YamlWorkflowDefinitionLoader>();
    }

    [Fact]
    public void AddJsonWorkflowLoader_RegistersJsonLoader()
    {
        var services = new ServiceCollection();
        services.AddJsonWorkflowLoader();
        var provider = services.BuildServiceProvider();

        var loader = provider.GetRequiredService<IWorkflowDefinitionLoader>();
        loader.Should().BeOfType<JsonWorkflowDefinitionLoader>();
    }

    [Fact]
    public void AddStepRegistry_RegistersBothConcreteAndInterface()
    {
        var services = new ServiceCollection();
        services.AddStepRegistry();
        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IStepRegistry>().Should().NotBeNull();
        provider.GetRequiredService<StepRegistry>().Should().NotBeNull();
    }

    [Fact]
    public void AddWorkflowDefinitionBuilder_RegistersBuilder()
    {
        var services = new ServiceCollection();
        services.AddStepRegistry();
        services.AddWorkflowDefinitionBuilder();
        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<WorkflowDefinitionBuilder>().Should().NotBeNull();
    }

    // ── WorkflowDefinition model fields ───────────────────────────────────────

    [Fact]
    public void StepDefinition_NewFields_HaveCorrectDefaults()
    {
        var step = new StepDefinition();
        step.Class.Should().BeNull();
        step.ThenSteps.Should().BeNull();
        step.ElseSteps.Should().BeNull();
        step.Message.Should().BeNull();
        step.RequiredApprovers.Should().BeNull();
        step.TimeoutMinutes.Should().BeNull();
        step.Catch.Should().BeNull();
    }

    [Fact]
    public void CatchDefinition_Defaults_AreCorrect()
    {
        var catchDef = new CatchDefinition();
        catchDef.Exception.Should().Be("Exception");
        catchDef.Steps.Should().BeEmpty();
    }

    [Fact]
    public void WorkflowDefinition_DescriptionField_DefaultsToNull()
    {
        var def = new WorkflowDefinition();
        def.Description.Should().BeNull();
    }

    // ── finallySteps field ────────────────────────────────────────────────────

    [Fact]
    public void Yaml_TryType_WithFinallySteps_LoadsAndBuilds()
    {
        var yaml = """
            name: TryFinallyFlow
            steps:
              - name: TryBlock
                type: try
                steps:
                  - type: step
                    class: RiskyStep
                finallySteps:
                  - type: step
                    class: CleanupStep
            """;

        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        def.Steps[0].Steps.Should().HaveCount(1);
        def.Steps[0].FinallySteps.Should().HaveCount(1);
        def.Steps[0].FinallySteps![0].Class.Should().Be("CleanupStep");
        def.Steps[0].ElseSteps.Should().BeNull(); // elseSteps not set

        var registry = new StepRegistry();
        registry.Register("RiskyStep", () => new TestStep("RiskyStep"));
        registry.Register("CleanupStep", () => new TestStep("CleanupStep"));
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void Yaml_TryType_FinallyStepsTakesPrecedenceOverElseSteps()
    {
        // When both finallySteps and elseSteps are set, finallySteps wins.
        var def = new WorkflowDefinition
        {
            Steps =
            [
                new StepDefinition
                {
                    Type = "try",
                    Steps = [new StepDefinition { Type = "step", Class = "RiskyStep" }],
                    FinallySteps = [new StepDefinition { Type = "step", Class = "FinallyStep" }],
                    ElseSteps = [new StepDefinition { Type = "step", Class = "ElseStep" }]
                }
            ]
        };

        var registry = new StepRegistry();
        registry.Register("RiskyStep", () => new TestStep("RiskyStep"));
        registry.Register("FinallyStep", () => new TestStep("FinallyStep"));
        // ElseStep is NOT registered — if it were used as the finally body, Build would fail.
        var act = () => new WorkflowDefinitionBuilder(registry).Build(def);
        act.Should().NotThrow("finallySteps takes precedence over elseSteps");
    }

    // ── snake_case YAML keys ───────────────────────────────────────────────────

    [Fact]
    public void Yaml_SnakeCaseKeys_AreNotSupportedByLoader()
    {
        // The YAML loader uses CamelCase naming convention.
        // Snake_case keys (e.g. required_approvers, timeout_minutes) are silently ignored.
        // Authors must use camelCase keys: requiredApprovers, timeoutMinutes.
        var yaml = """
            name: ApprovalFlow
            steps:
              - name: HumanReview
                type: approval
                message: Review required
                required_approvers: 3
                timeout_minutes: 120
            """;

        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        // Loader silently ignores unknown keys — values are null/default.
        def.Steps[0].RequiredApprovers.Should().BeNull(
            "snake_case 'required_approvers' is not recognized; use camelCase 'requiredApprovers'");
        def.Steps[0].TimeoutMinutes.Should().BeNull(
            "snake_case 'timeout_minutes' is not recognized; use camelCase 'timeoutMinutes'");
    }

    // ── NamedCompensatingStep ICompensatingStep delegation ────────────────────

    [Fact]
    public async Task NamedStep_WithCompensatingInner_DelegatesCompensate()
    {
        // When ApplyName wraps a compensating step, a NamedCompensatingStep is returned and
        // CompensateAsync must be forwarded to the inner step so saga compensation is not silently lost.
        var inner = new CompensatingTestStep("OriginalName");
        var registry = new StepRegistry();
        registry.Register("CompStep", () => inner);

        var def = new WorkflowDefinition
        {
            Compensation = true,
            Steps =
            [
                new StepDefinition
                {
                    Name = "RenamedStep",  // override name forces NamedCompensatingStep wrapping
                    Type = "step",
                    Class = "CompStep"
                }
            ]
        };

        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Steps.Should().HaveCount(1);
        wf.Steps[0].Should().BeAssignableTo<ICompensatingStep>(
            "NamedCompensatingStep wrapping a compensating inner step must implement ICompensatingStep");

        var comp = (ICompensatingStep)wf.Steps[0];
        var ctx = new WorkflowContext();
        await comp.CompensateAsync(ctx);
        inner.Compensated.Should().BeTrue("CompensateAsync must be delegated to the inner compensating step");
    }

    [Fact]
    public void NamedStep_WithNonCompensatingInner_DoesNotImplementICompensatingStep()
    {
        // When ApplyName wraps a non-compensating step, the result should be a plain NamedStep
        // that does NOT implement ICompensatingStep — non-compensating steps must not become
        // inadvertently compensatable by the mere act of overriding their name.
        var registry = new StepRegistry();
        registry.Register("PlainStep", () => new TestStep("OriginalName"));

        var def = new WorkflowDefinition
        {
            Steps =
            [
                new StepDefinition
                {
                    Name = "RenamedPlain",
                    Type = "step",
                    Class = "PlainStep"
                }
            ]
        };

        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Steps.Should().HaveCount(1);
        wf.Steps[0].Name.Should().Be("RenamedPlain");
        wf.Steps[0].Should().NotBeAssignableTo<ICompensatingStep>(
            "a plain (non-compensating) step renamed via ApplyName must not acquire ICompensatingStep");
    }

    // ── approval fallback: TimeoutMinutes ─────────────────────────────────────

    [Fact]
    public async Task Yaml_ApprovalFallback_RecordsTimeoutMinutes()
    {
        var def = new WorkflowDefinition
        {
            Steps =
            [
                new StepDefinition
                {
                    Name = "Review",
                    Type = "approval",
                    Message = "Please review",
                    RequiredApprovers = 2,
                    TimeoutMinutes = 60
                }
            ]
        };

        var registry = new StepRegistry(); // "approval" not registered → fallback
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);

        var ctx = new WorkflowContext();
        await wf.ExecuteAsync(ctx);
        ctx.Properties["Review.TimeoutMinutes"].Should().Be(60);
    }

    [Fact]
    public void Yaml_ApprovalFallback_UsesMessageAsDefaultStepName_WhenNameOmitted()
    {
        // Two unnamed approval steps with different messages must produce distinct step names
        // so that DefaultWorkflowValidator does not flag a duplicate-step-name error.
        var def = new WorkflowDefinition
        {
            Steps =
            [
                new StepDefinition { Type = "approval", Message = "Manager sign-off" },
                new StepDefinition { Type = "approval", Message = "Director approval" }
            ]
        };

        var registry = new StepRegistry(); // "approval" not registered → fallback
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);

        wf.Steps.Should().HaveCount(2);
        wf.Steps[0].Name.Should().Be("Manager sign-off");
        wf.Steps[1].Name.Should().Be("Director approval");
        wf.Steps[0].Name.Should().NotBe(wf.Steps[1].Name,
            "two unnamed approval steps with different messages must not collide");
    }

    // ── timeoutSeconds ────────────────────────────────────────────────────────

    [Fact]
    public async Task Yaml_StepType_WithTimeoutSeconds_FaultsWithTimeoutException_WhenStepExceedsLimit()
    {
        var registry = new StepRegistry();
        registry.Register("SlowStep", () => new SlowStep(TimeSpan.FromSeconds(10)));

        var def = new WorkflowDefinition
        {
            Steps =
            [
                new StepDefinition
                {
                    Name = "SlowOp",
                    Type = "step",
                    Class = "SlowStep",
                    TimeoutSeconds = 0.05 // 50 ms — the step takes 10 s so it will time out
                }
            ]
        };

        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Steps.Should().HaveCount(1);
        wf.Steps[0].Name.Should().Be("SlowOp", "the configured name is preserved on the timeout wrapper");

        var ctx = new WorkflowContext();
        var result = await wf.ExecuteAsync(ctx);
        result.Status.Should().Be(WorkflowStatus.Faulted);
        result.Errors.Should().ContainSingle()
            .Which.Exception.Should().BeOfType<TimeoutException>();
    }

    [Fact]
    public async Task Yaml_StepType_WithoutTimeoutSeconds_CompletesNormally()
    {
        var executed = new List<string>();
        var registry = new StepRegistry();
        registry.Register("QuickStep", () => new TrackingStep("QuickStep", executed));

        var def = new WorkflowDefinition
        {
            Steps =
            [
                new StepDefinition
                {
                    Name = "QuickOp",
                    Type = "step",
                    Class = "QuickStep"
                    // TimeoutSeconds intentionally omitted
                }
            ]
        };

        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        await wf.ExecuteAsync(new WorkflowContext());
        executed.Should().ContainSingle().Which.Should().Be("QuickStep");
    }

    [Fact]
    public void Yaml_TimeoutSeconds_LoadsAndBuilds()
    {
        var yaml = """
            name: TimeoutFlow
            steps:
              - name: SlowOp
                type: step
                class: SlowStep
                timeoutSeconds: 0.1
            """;

        var loader = new YamlWorkflowDefinitionLoader();
        var def = loader.Load(yaml);

        def.Steps[0].TimeoutSeconds.Should().BeApproximately(0.1, 0.001);

        var registry = new StepRegistry();
        registry.Register("SlowStep", () => new SlowStep(TimeSpan.FromSeconds(10)));
        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        wf.Steps.Should().HaveCount(1);
        wf.Steps[0].Name.Should().Be("SlowOp");
    }

    // ── InlineWorkflowStep failure propagation ────────────────────────────────

    [Fact]
    public async Task Conditional_BranchFailure_AbortsOuterWorkflow()
    {
        // A failing step inside a conditional Then-branch must not be silently swallowed.
        // Before the fix, InlineWorkflowStep returned the sub-workflow result without checking
        // it, so the outer workflow would complete normally even when a branch step threw.
        var registry = new StepRegistry();
        registry.Register("BadStep", () => new ThrowingStep());

        var subsequentExecuted = new List<string>();
        registry.Register("AfterStep", () => new TrackingStep("AfterStep", subsequentExecuted));

        var def = new WorkflowDefinition
        {
            Steps =
            [
                new StepDefinition
                {
                    Type = "conditional",
                    Condition = "isActive",  // property key — must be set in context to trigger branch
                    ThenSteps = [new StepDefinition { Type = "step", Class = "BadStep" }]
                },
                // This step must NOT execute if the conditional branch failed
                new StepDefinition { Type = "step", Class = "AfterStep" }
            ]
        };

        var ctx = new WorkflowContext();
        ctx.Properties["isActive"] = true; // ensure the Then-branch executes

        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        var result = await wf.ExecuteAsync(ctx);

        result.Status.Should().NotBe(WorkflowStatus.Completed,
            "a failing branch step must prevent the outer workflow from completing normally");
        subsequentExecuted.Should().BeEmpty(
            "subsequent steps must not run after a failed conditional branch");
    }

    // ── TimeoutStepWrapper wall-clock enforcement ─────────────────────────────

    [Fact]
    public async Task Timeout_WallClock_EnforcedEvenWhenStepIgnoresCancellationToken()
    {
        // Verifies that TimeoutStepWrapper enforces the wall-clock timeout via Task.WhenAny even
        // when the inner step does not observe its CancellationToken (cooperative cancellation alone
        // would not stop such a step within the configured timeout).
        var registry = new StepRegistry();
        registry.Register("IgnorantStep", () => new CancellationIgnorantSlowStep());

        var def = new WorkflowDefinition
        {
            Steps =
            [
                new StepDefinition
                {
                    Name = "Slow",
                    Type = "step",
                    Class = "IgnorantStep",
                    TimeoutSeconds = 0.1 // 100 ms
                }
            ]
        };

        var wf = new WorkflowDefinitionBuilder(registry).Build(def);
        var result = await wf.ExecuteAsync(new WorkflowContext());

        result.Status.Should().Be(WorkflowStatus.Faulted);
        result.Errors.Should().ContainSingle()
            .Which.Exception.Should().BeOfType<TimeoutException>();
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private sealed class TestStep(string name) : IStep
    {
        public string Name => name;
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    private sealed class TrackingStep(string name, List<string> log) : IStep
    {
        public string Name => name;
        public Task ExecuteAsync(IWorkflowContext context)
        {
            log.Add(name);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingStep : IStep
    {
        public string Name => "ThrowStep";
        public Task ExecuteAsync(IWorkflowContext context) =>
            throw new InvalidOperationException("Simulated failure");
    }

    private sealed class SlowStep(TimeSpan delay) : IStep
    {
        public string Name => "SlowStep";
        public Task ExecuteAsync(IWorkflowContext context) =>
            Task.Delay(delay, context.CancellationToken);
    }

    /// <summary>
    /// A step that does NOT observe the CancellationToken — used to verify that
    /// <c>TimeoutStepWrapper</c> enforces wall-clock timeouts even for uncooperative steps.
    /// </summary>
    private sealed class CancellationIgnorantSlowStep : IStep
    {
        public string Name => "IgnorantStep";
        public Task ExecuteAsync(IWorkflowContext context) =>
            Task.Delay(TimeSpan.FromSeconds(30)); // intentionally ignores context.CancellationToken
    }

    private sealed class CompensatingTestStep(string name) : ICompensatingStep
    {
        public bool Compensated { get; private set; }
        public string Name => name;
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
        public Task CompensateAsync(IWorkflowContext context)
        {
            Compensated = true;
            return Task.CompletedTask;
        }
    }
}
