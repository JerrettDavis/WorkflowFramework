using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Checkpointing;
using WorkflowFramework.Tests.TinyBDD.Support;

namespace WorkflowFramework.Tests.TinyBDD.Core.Checkpointing;

[Feature("WorkflowResumeEngine")]
public class WorkflowResumeEngineTests : TinyBddTestBase
{
    public WorkflowResumeEngineTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Resume runs workflow from beginning when no checkpoint exists"), Fact]
    public async Task ResumeWithNoCheckpointRunsFromStart()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var executed = new List<string>();
        var workflow = Workflow.Create("fresh")
            .Step(new Testing.LambdaStep("only-step", _ => { executed.Add("only-step"); return Task.CompletedTask; }))
            .Build();

        var engine = new WorkflowResumeEngine(store);
        var result = await engine.ResumeAsync("no-checkpoint-id", workflow);

        await Given("a fresh workflow with no checkpoint", () => (result, executed))
            .Then("the step executes and result is successful", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.executed.Should().ContainSingle();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Resume skips completed steps and continues from the saved index"), Fact]
    public async Task ResumeSkipsCompletedSteps()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var wfId = "resume-wf";
        await store.SaveAsync(wfId, 0, new Dictionary<string, object?> { ["step0done"] = true });

        var executed = new List<string>();
        var workflow = Workflow.Create("three-steps")
            .Step(new Testing.LambdaStep("step-0", _ => { executed.Add("step-0"); return Task.CompletedTask; }))
            .Step(new Testing.LambdaStep("step-1", _ => { executed.Add("step-1"); return Task.CompletedTask; }))
            .Step(new Testing.LambdaStep("step-2", _ => { executed.Add("step-2"); return Task.CompletedTask; }))
            .Build();

        var engine = new WorkflowResumeEngine(store);
        var result = await engine.ResumeAsync(wfId, workflow);

        await Given("a workflow resumed after step 0 was checkpointed", () => (result, executed))
            .Then("only steps 1 and 2 run", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.executed.Should().NotContain("step-0");
                t.executed.Should().Contain("step-1");
                t.executed.Should().Contain("step-2");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Resume clears checkpoint after successful completion"), Fact]
    public async Task ResumeClearsCheckpointOnSuccess()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var wfId = "clear-wf";
        await store.SaveAsync(wfId, 0, new Dictionary<string, object?>());
        var workflow = Workflow.Create("two-steps")
            .Step(new Testing.LambdaStep("step-0", _ => Task.CompletedTask))
            .Step(new Testing.LambdaStep("step-1", _ => Task.CompletedTask))
            .Build();

        var engine = new WorkflowResumeEngine(store);
        var result = await engine.ResumeAsync(wfId, workflow);
        var cp = await store.LoadAsync(wfId);

        await Given("a workflow that completed after resume", () => (result, cp))
            .Then("result is successful and checkpoint is removed", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.cp.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Resume returns Completed immediately when all steps were already checkpointed"), Fact]
    public async Task ResumeReturnsCompletedWhenAllStepsDone()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var wfId = "all-done-wf";
        // single-step workflow; step 0 is the only step — checkpointing it means all done
        await store.SaveAsync(wfId, 0, new Dictionary<string, object?> { ["done"] = true });
        var workflow = Workflow.Create("one-step")
            .Step(new Testing.LambdaStep("step-0", _ => Task.CompletedTask))
            .Build();

        var engine = new WorkflowResumeEngine(store);
        var result = await engine.ResumeAsync(wfId, workflow);

        await Given("a workflow where all steps were previously completed", () => result)
            .Then("result status is Completed", r =>
            {
                r.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }
}
