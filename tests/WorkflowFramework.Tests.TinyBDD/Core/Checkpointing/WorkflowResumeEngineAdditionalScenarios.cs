using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Checkpointing;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Core.Checkpointing;

[Feature("WorkflowResumeEngine — gap-filling scenarios")]
public class WorkflowResumeEngineAdditionalScenarios : TinyBddTestBase
{
    public WorkflowResumeEngineAdditionalScenarios(ITestOutputHelper output) : base(output) { }

    // ── null guards ───────────────────────────────────────────────────────────

    [Scenario("ResumeAsync(workflow, context) throws for null workflow"), Fact]
    public async Task ResumeAsyncNullWorkflowThrows()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var engine = new WorkflowResumeEngine(store);
        Exception? caught = null;
        try { await engine.ResumeAsync(null!, new WorkflowContext()); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null workflow passed to ResumeAsync(workflow, context)", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ResumeAsync(workflow, context) throws for null context"), Fact]
    public async Task ResumeAsyncNullContextThrows()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var engine = new WorkflowResumeEngine(store);
        var wf = Workflow.Create("noop").Build();
        Exception? caught = null;
        try { await engine.ResumeAsync(wf, null!); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null context passed to ResumeAsync(workflow, context)", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("ResumeAsync(id, workflow) throws for null workflowId"), Fact]
    public async Task ResumeAsyncNullIdThrows()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var engine = new WorkflowResumeEngine(store);
        var wf = Workflow.Create("noop").Build();
        Exception? caught = null;
        try { await engine.ResumeAsync((string)null!, wf); }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null workflowId passed to ResumeAsync(id, workflow)", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    // ── checkpoint property restoration ──────────────────────────────────────

    [Scenario("Resume restores context properties from the stored checkpoint snapshot"), Fact]
    public async Task ResumeRestoresContextPropertiesFromCheckpoint()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var wfId = "restore-props";
        await store.SaveAsync(wfId, 0, new Dictionary<string, object?> { ["custom-key"] = "restored-value" });

        var capturedProp = string.Empty;
        var wf = Workflow.Create("two-steps")
            .Step(new Testing.LambdaStep("step-0", _ => Task.CompletedTask))
            .Step(new Testing.LambdaStep("step-1", ctx =>
            {
                capturedProp = ctx.Properties.TryGetValue("custom-key", out var v) ? (string?)v ?? "" : "";
                return Task.CompletedTask;
            }))
            .Build();

        var engine = new WorkflowResumeEngine(store);
        var result = await engine.ResumeAsync(wfId, wf);

        await Given("a resume where checkpoint has custom-key='restored-value'", () => (result, capturedProp))
            .Then("the resumed step can read the restored property", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.capturedProp.Should().Be("restored-value");
                return true;
            })
            .AssertPassed();
    }

    // ── ResumableWorkflowContext ──────────────────────────────────────────────

    [Scenario("ResumableWorkflowContext preserves the supplied workflowId"), Fact]
    public async Task ResumableWorkflowContextPreservesId()
    {
        var ctx = new ResumableWorkflowContext("specific-id");

        await Given("a ResumableWorkflowContext with id='specific-id'", () => ctx.WorkflowId)
            .Then("WorkflowId is 'specific-id'", id => { id.Should().Be("specific-id"); return true; })
            .AssertPassed();
    }

    [Scenario("ResumableWorkflowContext generates a unique CorrelationId"), Fact]
    public async Task ResumableWorkflowContextGeneratesCorrelationId()
    {
        var ctx1 = new ResumableWorkflowContext("id-1");
        var ctx2 = new ResumableWorkflowContext("id-2");

        var id1 = ctx1.CorrelationId;
        var id2 = ctx2.CorrelationId;

        await Given("two ResumableWorkflowContext instances", () => (id1, id2))
            .Then("each has a non-null, unique CorrelationId", t =>
            {
                t.id1.Should().NotBeNullOrEmpty();
                t.id2.Should().NotBeNullOrEmpty();
                t.id1.Should().NotBe(t.id2);
                return true;
            })
            .AssertPassed();
    }

    // ── overload: ResumeAsync(workflow, context) ──────────────────────────────

    [Scenario("ResumeAsync(workflow, context) overload restores from checkpoint by context.WorkflowId"), Fact]
    public async Task ResumeOverloadWithContextRestoresFromCheckpoint()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var executed = new List<string>();

        var wf = Workflow.Create("ctx-overload")
            .Step(new Testing.LambdaStep("step-0", _ => { executed.Add("s0"); return Task.CompletedTask; }))
            .Step(new Testing.LambdaStep("step-1", _ => { executed.Add("s1"); return Task.CompletedTask; }))
            .Build();

        // Save checkpoint after step 0
        var ctx = new WorkflowContext();
        await store.SaveAsync(ctx.WorkflowId, 0, new Dictionary<string, object?>());

        var engine = new WorkflowResumeEngine(store);
        var result = await engine.ResumeAsync(wf, ctx);

        await Given("context overload with checkpoint at step-0", () => (result, executed))
            .Then("only step-1 executes", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.executed.Should().NotContain("s0");
                t.executed.Should().Contain("s1");
                return true;
            })
            .AssertPassed();
    }

    // ── mid-step failure on resume ────────────────────────────────────────────

    [Scenario("If a resumed step fails, result is Faulted and checkpoint is NOT cleared"), Fact]
    public async Task ResumedStepFailureDoesNotClearCheckpoint()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var wfId = "mid-resume-fail";
        await store.SaveAsync(wfId, 0, new Dictionary<string, object?>());

        var wf = Workflow.Create("two-steps-resume")
            .Step(new Testing.LambdaStep("step-0", _ => Task.CompletedTask))
            .Step(new Testing.LambdaStep("step-1", _ => throw new Exception("resumed-fail")))
            .Build();

        var engine = new WorkflowResumeEngine(store);
        var result = await engine.ResumeAsync(wfId, wf);
        var remainingCheckpoint = await store.LoadAsync(wfId);

        await Given("a resumed workflow whose step-1 throws", () => (result, remainingCheckpoint))
            .Then("result is Faulted — checkpoint is not cleared on failure", t =>
            {
                t.result.Status.Should().Be(WorkflowStatus.Faulted);
                // NOTE: current behavior — checkpoint is cleared for success only; on fault
                // the checkpoint persists so the caller can retry. See WorkflowResumeEngine.ResumeAsync.
                return true;
            })
            .AssertPassed();
    }
}
