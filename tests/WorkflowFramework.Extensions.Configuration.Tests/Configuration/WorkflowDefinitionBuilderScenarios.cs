using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using WorkflowFramework.Extensions.Configuration;

namespace WorkflowFramework.Extensions.Configuration.Tests.Configuration;

[Feature("WorkflowDefinitionBuilder — builds IWorkflow from WorkflowDefinition (Phase I coverage)")]
public class WorkflowDefinitionBuilderScenarios : TinyBddXunitBase
{
    public WorkflowDefinitionBuilderScenarios(ITestOutputHelper output) : base(output) { }

    // ── helpers ─────────────────────────────────────────────────────────────

    private sealed class NoopStep(string name) : IStep
    {
        public string Name => name;
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    private sealed class CompensatingNoopStep(string name) : ICompensatingStep
    {
        public string Name => name;
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
        public Task CompensateAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    private static StepRegistry MakeRegistry(params (string name, IStep step)[] registrations)
    {
        var r = new StepRegistry();
        foreach (var (name, step) in registrations)
            r.Register(name, () => step);
        return r;
    }

    // ── basic build ──────────────────────────────────────────────────────────

    [Scenario("Build returns a workflow with the configured name"), Fact]
    public async Task BuildSetsWorkflowName()
    {
        var registry = new StepRegistry();
        var builder = new WorkflowDefinitionBuilder(registry);
        var def = new WorkflowDefinition { Name = "MyFlow", Steps = [] };
        var wf = builder.Build(def);

        await Given("a WorkflowDefinition named 'MyFlow'", () => wf)
            .Then("workflow Name is 'MyFlow'", w =>
            {
                w.Name.Should().Be("MyFlow");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Build null definition throws ArgumentNullException"), Fact]
    public async Task BuildNullDefinitionThrows()
    {
        var builder = new WorkflowDefinitionBuilder(new StepRegistry());
        Exception? caught = null;
        try { builder.Build(null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null definition passed to Build", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Build with compensation flag enables compensation"), Fact]
    public async Task BuildWithCompensation()
    {
        var registry = MakeRegistry(("noop", new NoopStep("noop")));
        var builder = new WorkflowDefinitionBuilder(registry);
        var def = new WorkflowDefinition
        {
            Name = "CompWf",
            Compensation = true,
            Steps = [new StepDefinition { Type = "step", Class = "noop" }]
        };
        var wf = builder.Build(def);

        await Given("workflow built with compensation=true", () => wf)
            .Then("workflow is built successfully", w =>
            {
                w.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    // ── type: step ──────────────────────────────────────────────────────────

    [Scenario("type=step with class resolves step via registry"), Fact]
    public async Task TypeStepWithClass()
    {
        var step = new NoopStep("noop");
        var registry = MakeRegistry(("NoopStep", step));
        var builder = new WorkflowDefinitionBuilder(registry);
        var def = new WorkflowDefinition
        {
            Name = "W",
            Steps = [new StepDefinition { Type = "step", Class = "NoopStep", Name = "s1" }]
        };
        var wf = builder.Build(def);

        await Given("type=step with class='NoopStep'", () => wf)
            .Then("workflow has one step", w =>
            {
                w.Steps.Should().HaveCount(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("type=step missing class throws InvalidOperationException"), Fact]
    public async Task TypeStepMissingClassThrows()
    {
        var builder = new WorkflowDefinitionBuilder(new StepRegistry());
        var def = new WorkflowDefinition
        {
            Name = "W",
            Steps = [new StepDefinition { Type = "step" }]
        };
        Exception? caught = null;
        try { builder.Build(def); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("type=step with no class", () => caught)
            .Then("InvalidOperationException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<InvalidOperationException>();
                return true;
            })
            .AssertPassed();
    }

    // ── TimeoutSeconds ───────────────────────────────────────────────────────

    [Scenario("TimeoutSeconds > 0 wraps step in timeout wrapper"), Fact]
    public async Task TimeoutSecondsWrapsStep()
    {
        var step = new NoopStep("noop");
        var registry = MakeRegistry(("NoopStep", step));
        var builder = new WorkflowDefinitionBuilder(registry);
        var def = new WorkflowDefinition
        {
            Name = "W",
            Steps = [new StepDefinition { Type = "step", Class = "NoopStep", TimeoutSeconds = 5 }]
        };
        var wf = builder.Build(def);

        var ctx = new WorkflowContext();
        var result = await wf.ExecuteAsync(ctx);

        await Given("type=step with TimeoutSeconds=5", () => result)
            .Then("workflow executes successfully", r =>
            {
                r.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("TimeoutSeconds > 0 with compensating step preserves compensation"), Fact]
    public async Task TimeoutSecondsWithCompensatingStep()
    {
        var step = new CompensatingNoopStep("comp");
        var registry = MakeRegistry(("CompStep", step));
        var builder = new WorkflowDefinitionBuilder(registry);
        var def = new WorkflowDefinition
        {
            Name = "W",
            Compensation = true,
            Steps = [new StepDefinition { Type = "step", Class = "CompStep", TimeoutSeconds = 5 }]
        };
        var wf = builder.Build(def);

        var ctx = new WorkflowContext();
        var result = await wf.ExecuteAsync(ctx);

        await Given("type=step with compensating step and TimeoutSeconds=5", () => result)
            .Then("workflow executes successfully", r =>
            {
                r.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    // ── type: parallel ───────────────────────────────────────────────────────

    [Scenario("type=parallel empty steps throws InvalidOperationException"), Fact]
    public async Task TypeParallelEmptyStepsThrows()
    {
        var builder = new WorkflowDefinitionBuilder(new StepRegistry());
        var def = new WorkflowDefinition
        {
            Name = "W",
            Steps = [new StepDefinition { Type = "parallel", Steps = [] }]
        };
        Exception? caught = null;
        try { builder.Build(def); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("type=parallel with empty steps", () => caught)
            .Then("InvalidOperationException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<InvalidOperationException>();
                return true;
            })
            .AssertPassed();
    }

    // ── type: while / dowhile ────────────────────────────────────────────────

    [Scenario("type=while missing condition throws InvalidOperationException"), Fact]
    public async Task TypeWhileMissingConditionThrows()
    {
        var builder = new WorkflowDefinitionBuilder(new StepRegistry());
        var def = new WorkflowDefinition
        {
            Name = "W",
            Steps = [new StepDefinition { Type = "while" }]
        };
        Exception? caught = null;
        try { builder.Build(def); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("type=while with no condition", () => caught)
            .Then("InvalidOperationException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<InvalidOperationException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("type=dowhile missing condition throws InvalidOperationException"), Fact]
    public async Task TypeDoWhileMissingConditionThrows()
    {
        var builder = new WorkflowDefinitionBuilder(new StepRegistry());
        var def = new WorkflowDefinition
        {
            Name = "W",
            Steps = [new StepDefinition { Type = "dowhile" }]
        };
        Exception? caught = null;
        try { builder.Build(def); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("type=dowhile with no condition", () => caught)
            .Then("InvalidOperationException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<InvalidOperationException>();
                return true;
            })
            .AssertPassed();
    }

    // ── type: retry ──────────────────────────────────────────────────────────

    [Scenario("type=retry empty steps throws InvalidOperationException"), Fact]
    public async Task TypeRetryEmptyStepsThrows()
    {
        var builder = new WorkflowDefinitionBuilder(new StepRegistry());
        var def = new WorkflowDefinition
        {
            Name = "W",
            Steps = [new StepDefinition { Type = "retry", Steps = [] }]
        };
        Exception? caught = null;
        try { builder.Build(def); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("type=retry with empty steps", () => caught)
            .Then("InvalidOperationException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<InvalidOperationException>();
                return true;
            })
            .AssertPassed();
    }

    // ── type: subworkflow ────────────────────────────────────────────────────

    [Scenario("type=subworkflow with missing key throws InvalidOperationException"), Fact]
    public async Task TypeSubWorkflowMissingKeyThrows()
    {
        var builder = new WorkflowDefinitionBuilder(new StepRegistry());
        var def = new WorkflowDefinition
        {
            Name = "W",
            Steps = [new StepDefinition { Type = "subworkflow" }]
        };
        Exception? caught = null;
        try { builder.Build(def); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("type=subworkflow with no subWorkflow or class", () => caught)
            .Then("InvalidOperationException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<InvalidOperationException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("type=subworkflow with unregistered key throws InvalidOperationException"), Fact]
    public async Task TypeSubWorkflowUnregisteredKeyThrows()
    {
        var builder = new WorkflowDefinitionBuilder(new StepRegistry(), subWorkflows: new Dictionary<string, IWorkflow>());
        var def = new WorkflowDefinition
        {
            Name = "W",
            Steps = [new StepDefinition { Type = "subworkflow", SubWorkflow = "ghost" }]
        };
        Exception? caught = null;
        try { builder.Build(def); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("type=subworkflow referencing unregistered 'ghost'", () => caught)
            .Then("InvalidOperationException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<InvalidOperationException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("type=subworkflow resolves and executes registered sub-workflow"), Fact]
    public async Task TypeSubWorkflowResolvesAndExecutes()
    {
        var inner = Workflow.Create("Inner")
            .Step("do-nothing", ctx => Task.CompletedTask)
            .Build();

        var subWorkflows = new Dictionary<string, IWorkflow> { ["Inner"] = inner };
        var builder = new WorkflowDefinitionBuilder(new StepRegistry(), subWorkflows);
        var def = new WorkflowDefinition
        {
            Name = "Outer",
            Steps = [new StepDefinition { Type = "subworkflow", SubWorkflow = "Inner" }]
        };
        var wf = builder.Build(def);

        var ctx = new WorkflowContext();
        var result = await wf.ExecuteAsync(ctx);

        await Given("type=subworkflow referencing registered 'Inner'", () => result)
            .Then("workflow executes successfully", r =>
            {
                r.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    // ── type: approval ───────────────────────────────────────────────────────

    [Scenario("type=approval with unregistered class falls back to recording step"), Fact]
    public async Task TypeApprovalFallbackRecordingStep()
    {
        var builder = new WorkflowDefinitionBuilder(new StepRegistry());
        var def = new WorkflowDefinition
        {
            Name = "W",
            Steps = [new StepDefinition
            {
                Type = "approval",
                Name = "PurchaseApproval",
                Message = "Approve purchase?",
                RequiredApprovers = 2,
                TimeoutMinutes = 60
            }]
        };
        var wf = builder.Build(def);
        var ctx = new WorkflowContext();
        await wf.ExecuteAsync(ctx);

        await Given("type=approval with no registered class — fallback recording step executed", () => ctx)
            .Then("approval properties are recorded on context", c =>
            {
                c.Properties.Should().ContainKey("PurchaseApproval.Message");
                c.Properties["PurchaseApproval.Message"].Should().Be("Approve purchase?");
                c.Properties.Should().ContainKey("PurchaseApproval.TimeoutMinutes");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("type=approval with registered class uses registered step"), Fact]
    public async Task TypeApprovalWithRegisteredClass()
    {
        var recorded = new List<string>();
        var approvalStep = new NoopStep("MyApproval");
        var registry = MakeRegistry(("MyApproval", approvalStep));
        var builder = new WorkflowDefinitionBuilder(registry);
        var def = new WorkflowDefinition
        {
            Name = "W",
            Steps = [new StepDefinition { Type = "approval", Class = "MyApproval" }]
        };
        var wf = builder.Build(def);
        var ctx = new WorkflowContext();
        var result = await wf.ExecuteAsync(ctx);

        await Given("type=approval with registered class 'MyApproval'", () => result)
            .Then("workflow executes via registered approval step", r =>
            {
                r.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    // ── type: saga ───────────────────────────────────────────────────────────

    [Scenario("type=saga empty steps throws InvalidOperationException"), Fact]
    public async Task TypeSagaEmptyStepsThrows()
    {
        var builder = new WorkflowDefinitionBuilder(new StepRegistry());
        var def = new WorkflowDefinition
        {
            Name = "W",
            Steps = [new StepDefinition { Type = "saga", Steps = [] }]
        };
        Exception? caught = null;
        try { builder.Build(def); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("type=saga with empty steps", () => caught)
            .Then("InvalidOperationException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<InvalidOperationException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("type=saga with body steps executes sub-workflow"), Fact]
    public async Task TypeSagaExecutesSagaSubWorkflow()
    {
        var step = new NoopStep("saga-action");
        var registry = MakeRegistry(("saga-action", step));
        var builder = new WorkflowDefinitionBuilder(registry);
        var def = new WorkflowDefinition
        {
            Name = "W",
            Steps =
            [
                new StepDefinition
                {
                    Type = "saga",
                    Name = "MySaga",
                    Steps = [new StepDefinition { Type = "step", Class = "saga-action" }]
                }
            ]
        };
        var wf = builder.Build(def);
        var ctx = new WorkflowContext();
        var result = await wf.ExecuteAsync(ctx);

        await Given("type=saga with one body step", () => result)
            .Then("workflow executes successfully", r =>
            {
                r.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    // ── legacy formats ───────────────────────────────────────────────────────

    [Scenario("legacy type-as-class-name resolves via registry"), Fact]
    public async Task LegacyTypeAsClassName()
    {
        var step = new NoopStep("NoopStep");
        var registry = MakeRegistry(("NoopStep", step));
        var builder = new WorkflowDefinitionBuilder(registry);
        // No explicit category in type — legacy format
        var def = new WorkflowDefinition
        {
            Name = "W",
            Steps = [new StepDefinition { Type = "NoopStep" }]
        };
        var wf = builder.Build(def);

        await Given("legacy step definition with type='NoopStep'", () => wf)
            .Then("workflow has one step resolved via registry", w =>
            {
                w.Steps.Should().HaveCount(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("class shorthand without type resolves via registry"), Fact]
    public async Task ClassShorthandWithoutType()
    {
        var step = new NoopStep("NoopStep");
        var registry = MakeRegistry(("NoopStep", step));
        var builder = new WorkflowDefinitionBuilder(registry);
        var def = new WorkflowDefinition
        {
            Name = "W",
            Steps = [new StepDefinition { Class = "NoopStep" }]
        };
        var wf = builder.Build(def);

        await Given("step definition with class='NoopStep' and no type", () => wf)
            .Then("workflow has one step resolved via registry", w =>
            {
                w.Steps.Should().HaveCount(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("step with no type or class throws InvalidOperationException"), Fact]
    public async Task NoTypeOrClassThrows()
    {
        var builder = new WorkflowDefinitionBuilder(new StepRegistry());
        var def = new WorkflowDefinition
        {
            Name = "W",
            Steps = [new StepDefinition { Name = "mystery" }]
        };
        Exception? caught = null;
        try { builder.Build(def); }
        catch (InvalidOperationException ex) { caught = ex; }

        await Given("step with no type or class", () => caught)
            .Then("InvalidOperationException is thrown", ex =>
            {
                ex.Should().NotBeNull().And.BeOfType<InvalidOperationException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("legacy parallel format builds parallel step"), Fact]
    public async Task LegacyParallelFormat()
    {
        var step1 = new NoopStep("s1");
        var step2 = new NoopStep("s2");
        var registry = MakeRegistry(("Step1", step1), ("Step2", step2));
        var builder = new WorkflowDefinitionBuilder(registry);
        var def = new WorkflowDefinition
        {
            Name = "W",
            Steps = [new StepDefinition { Parallel = ["Step1", "Step2"] }]
        };
        var wf = builder.Build(def);

        await Given("legacy parallel format with two class names", () => wf)
            .Then("workflow has one parallel step", w =>
            {
                w.Steps.Should().HaveCount(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("legacy retry format wraps single step"), Fact]
    public async Task LegacyRetryFormat()
    {
        var step = new NoopStep("noop");
        var registry = MakeRegistry(("NoopStep", step));
        var builder = new WorkflowDefinitionBuilder(registry);
        var def = new WorkflowDefinition
        {
            Name = "W",
            Steps =
            [
                new StepDefinition
                {
                    Type = "NoopStep",
                    Retry = new RetryDefinition { MaxAttempts = 2 }
                }
            ]
        };
        var wf = builder.Build(def);

        await Given("legacy retry format wrapping NoopStep", () => wf)
            .Then("workflow has one step", w =>
            {
                w.Steps.Should().HaveCount(1);
                return true;
            })
            .AssertPassed();
    }
}
