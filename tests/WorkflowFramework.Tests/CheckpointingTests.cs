using FluentAssertions;
using WorkflowFramework.Checkpointing;
using Xunit;

namespace WorkflowFramework.Tests;

public class CheckpointingTests
{
    [Fact]
    public async Task InMemoryStore_SaveAndLoad_ReturnsCheckpoint()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var props = new Dictionary<string, object?> { ["key"] = "value" };

        await store.SaveAsync("wf-1", 2, props);

        var checkpoint = await store.LoadAsync("wf-1");
        checkpoint.Should().NotBeNull();
        checkpoint!.WorkflowId.Should().Be("wf-1");
        checkpoint.StepIndex.Should().Be(2);
        checkpoint.ContextSnapshot["key"].Should().Be("value");
    }

    [Fact]
    public async Task InMemoryStore_Load_NonExistent_ReturnsNull()
    {
        var store = new InMemoryWorkflowCheckpointStore();

        var checkpoint = await store.LoadAsync("does-not-exist");
        checkpoint.Should().BeNull();
    }

    [Fact]
    public async Task InMemoryStore_Clear_RemovesCheckpoint()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        await store.SaveAsync("wf-1", 0, new Dictionary<string, object?>());

        await store.ClearAsync("wf-1");

        var checkpoint = await store.LoadAsync("wf-1");
        checkpoint.Should().BeNull();
    }

    [Fact]
    public async Task InMemoryStore_Save_OverwritesPrevious()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        await store.SaveAsync("wf-1", 0, new Dictionary<string, object?> { ["v"] = 1 });
        await store.SaveAsync("wf-1", 1, new Dictionary<string, object?> { ["v"] = 2 });

        var checkpoint = await store.LoadAsync("wf-1");
        checkpoint!.StepIndex.Should().Be(1);
        checkpoint.ContextSnapshot["v"].Should().Be(2);
    }

    [Fact]
    public async Task WithCheckpointing_SavesAfterEachStep()
    {
        var store = new InMemoryWorkflowCheckpointStore();

        var workflow = Workflow.Create("test")
            .Step("Step1", ctx => { ctx.Properties["a"] = 1; return Task.CompletedTask; })
            .Step("Step2", ctx => { ctx.Properties["b"] = 2; return Task.CompletedTask; })
            .WithCheckpointing(store)
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();

        // Last checkpoint should be from Step2 (index 1)
        var checkpoint = await store.LoadAsync(context.WorkflowId);
        checkpoint.Should().NotBeNull();
        checkpoint!.StepIndex.Should().Be(1);
        checkpoint.ContextSnapshot["a"].Should().Be(1);
        checkpoint.ContextSnapshot["b"].Should().Be(2);
    }

    [Fact]
    public async Task WithCheckpointing_OnFailure_CheckpointIsAtLastSuccessfulStep()
    {
        var store = new InMemoryWorkflowCheckpointStore();

        var workflow = Workflow.Create("test")
            .Step("Step1", ctx => { ctx.Properties["a"] = 1; return Task.CompletedTask; })
            .Step("Step2", _ => throw new InvalidOperationException("boom"))
            .Step("Step3", ctx => { ctx.Properties["c"] = 3; return Task.CompletedTask; })
            .WithCheckpointing(store)
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        result.Status.Should().Be(WorkflowStatus.Faulted);

        // Checkpoint should be at Step1 (index 0) since Step2 failed
        var checkpoint = await store.LoadAsync(context.WorkflowId);
        checkpoint.Should().NotBeNull();
        checkpoint!.StepIndex.Should().Be(0);
        checkpoint.ContextSnapshot["a"].Should().Be(1);
    }

    [Fact]
    public async Task ResumeAsync_SkipsCompletedSteps()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var step1Executed = false;
        var step2Executed = false;
        var step3Executed = false;

        var workflow = Workflow.Create("test")
            .Step("Step1", ctx => { step1Executed = true; ctx.Properties["a"] = 1; return Task.CompletedTask; })
            .Step("Step2", ctx => { step2Executed = true; ctx.Properties["b"] = 2; return Task.CompletedTask; })
            .Step("Step3", ctx => { step3Executed = true; ctx.Properties["c"] = 3; return Task.CompletedTask; })
            .Build();

        // Simulate a checkpoint after Step1 (index 0)
        var workflowId = "resume-test";
        await store.SaveAsync(workflowId, 0, new Dictionary<string, object?> { ["a"] = 1 });

        var engine = new WorkflowResumeEngine(store);
        var result = await engine.ResumeAsync(workflowId, workflow);

        result.IsSuccess.Should().BeTrue();
        step1Executed.Should().BeFalse("Step1 was already checkpointed");
        step2Executed.Should().BeTrue("Step2 should resume");
        step3Executed.Should().BeTrue("Step3 should execute");
        result.Context.Properties["a"].Should().Be(1, "restored from checkpoint");
    }

    [Fact]
    public async Task ResumeAsync_NoCheckpoint_RunsFromBeginning()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var executed = new List<string>();

        var workflow = Workflow.Create("test")
            .Step("Step1", ctx => { executed.Add("Step1"); return Task.CompletedTask; })
            .Step("Step2", ctx => { executed.Add("Step2"); return Task.CompletedTask; })
            .Build();

        var engine = new WorkflowResumeEngine(store);
        var result = await engine.ResumeAsync("no-checkpoint", workflow);

        result.IsSuccess.Should().BeTrue();
        executed.Should().BeEquivalentTo(["Step1", "Step2"]);
    }

    [Fact]
    public async Task ResumeAsync_AllStepsCompleted_ReturnsCompleted()
    {
        var store = new InMemoryWorkflowCheckpointStore();

        var workflow = Workflow.Create("test")
            .Step("Step1", _ => Task.CompletedTask)
            .Step("Step2", _ => Task.CompletedTask)
            .Build();

        // Checkpoint at last step (index 1 = Step2)
        await store.SaveAsync("done-wf", 1, new Dictionary<string, object?>());

        var engine = new WorkflowResumeEngine(store);
        var result = await engine.ResumeAsync("done-wf", workflow);

        result.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    public async Task ResumeAsync_ClearsCheckpointOnSuccess()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        await store.SaveAsync("wf-clear", 0, new Dictionary<string, object?>());

        var workflow = Workflow.Create("test")
            .Step("Step1", _ => Task.CompletedTask)
            .Step("Step2", _ => Task.CompletedTask)
            .Build();

        var engine = new WorkflowResumeEngine(store);
        await engine.ResumeAsync("wf-clear", workflow);

        var checkpoint = await store.LoadAsync("wf-clear");
        checkpoint.Should().BeNull("checkpoint should be cleared after successful resume");
    }

    [Fact]
    public async Task ResumeAsync_WithContext_RestoresProperties()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var workflowId = "ctx-test";

        await store.SaveAsync(workflowId, 0, new Dictionary<string, object?> { ["restored"] = true });

        object? restoredValue = null;
        var workflow = Workflow.Create("test")
            .Step("Step1", _ => Task.CompletedTask)
            .Step("Step2", ctx => { restoredValue = ctx.Properties["restored"]; return Task.CompletedTask; })
            .Build();

        var context = new ResumableWorkflowContext(workflowId);
        var engine = new WorkflowResumeEngine(store);
        var result = await engine.ResumeAsync(workflow, context);

        result.IsSuccess.Should().BeTrue();
        restoredValue.Should().Be(true);
    }

    [Fact]
    public async Task FullScenario_FailThenResume()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var attempt = 0;

        IWorkflow BuildWorkflow() => Workflow.Create("order-process")
            .Step("ValidateOrder", ctx => { ctx.Properties["validated"] = true; return Task.CompletedTask; })
            .Step("ChargePayment", ctx =>
            {
                attempt++;
                if (attempt == 1) throw new InvalidOperationException("Payment gateway down");
                ctx.Properties["charged"] = true;
                return Task.CompletedTask;
            })
            .Step("ShipOrder", ctx => { ctx.Properties["shipped"] = true; return Task.CompletedTask; })
            .WithCheckpointing(store)
            .Build();

        // First attempt — fails at ChargePayment
        var context = new WorkflowContext();
        var workflowId = context.WorkflowId;
        var result1 = await BuildWorkflow().ExecuteAsync(context);
        result1.Status.Should().Be(WorkflowStatus.Faulted);

        // Checkpoint should be at ValidateOrder (index 0)
        var checkpoint = await store.LoadAsync(workflowId);
        checkpoint.Should().NotBeNull();
        checkpoint!.StepIndex.Should().Be(0);

        // Resume — ChargePayment succeeds this time
        var resumeContext = new ResumableWorkflowContext(workflowId);
        var engine = new WorkflowResumeEngine(store);
        var result2 = await engine.ResumeAsync(BuildWorkflow(), resumeContext);

        result2.IsSuccess.Should().BeTrue();
        result2.Context.Properties["validated"].Should().Be(true, "restored from checkpoint");
        result2.Context.Properties["charged"].Should().Be(true);
        result2.Context.Properties["shipped"].Should().Be(true);
    }

    [Fact]
    public async Task InMemoryStore_SnapshotsAreIsolated()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var props = new Dictionary<string, object?> { ["key"] = "original" };

        await store.SaveAsync("wf-1", 0, props);

        // Mutate original — should not affect stored checkpoint
        props["key"] = "mutated";

        var checkpoint = await store.LoadAsync("wf-1");
        checkpoint!.ContextSnapshot["key"].Should().Be("original");
    }
}
