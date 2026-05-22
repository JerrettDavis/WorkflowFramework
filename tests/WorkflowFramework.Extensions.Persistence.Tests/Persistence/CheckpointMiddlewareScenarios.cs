using FluentAssertions;
using NSubstitute;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using WorkflowFramework.Extensions.Persistence;
using WorkflowFramework.Persistence;

namespace WorkflowFramework.Extensions.Persistence.Tests.Persistence;

[Feature("CheckpointMiddleware — saves workflow state after each step")]
public class CheckpointMiddlewareScenarios : TinyBddXunitBase
{
    public CheckpointMiddlewareScenarios(ITestOutputHelper output) : base(output) { }

    private static IWorkflowStateStore MakeStore() => Substitute.For<IWorkflowStateStore>();

    private static IWorkflowContext MakeContext(string workflowId = "wf-1", int stepIndex = 2)
    {
        var ctx = Substitute.For<IWorkflowContext>();
        ctx.WorkflowId.Returns(workflowId);
        ctx.CorrelationId.Returns("corr-1");
        ctx.CurrentStepIndex.Returns(stepIndex);
        ctx.Properties.Returns(new Dictionary<string, object?> { ["key"] = "value" });
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    [Scenario("constructor rejects null store"), Fact]
    public async Task ConstructorRejectsNullStore()
    {
        await Given("a null store reference", () => new Action(() => new CheckpointMiddleware(null!)))
            .Then("constructor throws ArgumentNullException with parameter name 'store'", act =>
            {
                act.Should().Throw<ArgumentNullException>().WithParameterName("store");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("next delegate is called before saving checkpoint"), Fact]
    public async Task NextCalledBeforeSaving()
    {
        var callOrder = new List<string>();
        var store = MakeStore();
        store.SaveCheckpointAsync(Arg.Any<string>(), Arg.Any<WorkflowState>(), Arg.Any<CancellationToken>())
             .Returns(_ => { callOrder.Add("save"); return Task.CompletedTask; });

        var sut = new CheckpointMiddleware(store);
        var ctx = MakeContext();
        var step = Substitute.For<IStep>();

        await sut.InvokeAsync(ctx, step, c => { callOrder.Add("next"); return Task.CompletedTask; });

        await Given("next and save call order recorded", () => callOrder)
            .Then("next was called before save", order =>
            {
                order.Should().ContainInOrder("next", "save");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("saved state captures workflow id"), Fact]
    public async Task SavedStateCapturesWorkflowId()
    {
        WorkflowState? captured = null;
        var store = MakeStore();
        store.SaveCheckpointAsync(Arg.Any<string>(), Arg.Any<WorkflowState>(), Arg.Any<CancellationToken>())
             .Returns(ci => { captured = ci.Arg<WorkflowState>(); return Task.CompletedTask; });

        var sut = new CheckpointMiddleware(store);
        var ctx = MakeContext("my-workflow");
        var step = Substitute.For<IStep>();

        await sut.InvokeAsync(ctx, step, _ => Task.CompletedTask);

        await Given("state saved for workflowId 'my-workflow'", () => captured!)
            .Then("captured WorkflowId is 'my-workflow'", state =>
            {
                state.WorkflowId.Should().Be("my-workflow");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("saved state captures last completed step index"), Fact]
    public async Task SavedStateCapturesStepIndex()
    {
        WorkflowState? captured = null;
        var store = MakeStore();
        store.SaveCheckpointAsync(Arg.Any<string>(), Arg.Any<WorkflowState>(), Arg.Any<CancellationToken>())
             .Returns(ci => { captured = ci.Arg<WorkflowState>(); return Task.CompletedTask; });

        var sut = new CheckpointMiddleware(store);
        var ctx = MakeContext(stepIndex: 5);
        var step = Substitute.For<IStep>();

        await sut.InvokeAsync(ctx, step, _ => Task.CompletedTask);

        await Given("state saved after step 5", () => captured!)
            .Then("LastCompletedStepIndex is 5", state =>
            {
                state.LastCompletedStepIndex.Should().Be(5);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("saved state has Running status"), Fact]
    public async Task SavedStateHasRunningStatus()
    {
        WorkflowState? captured = null;
        var store = MakeStore();
        store.SaveCheckpointAsync(Arg.Any<string>(), Arg.Any<WorkflowState>(), Arg.Any<CancellationToken>())
             .Returns(ci => { captured = ci.Arg<WorkflowState>(); return Task.CompletedTask; });

        var sut = new CheckpointMiddleware(store);
        var ctx = MakeContext();
        var step = Substitute.For<IStep>();

        await sut.InvokeAsync(ctx, step, _ => Task.CompletedTask);

        await Given("state saved during workflow execution", () => captured!)
            .Then("status is Running", state =>
            {
                state.Status.Should().Be(WorkflowStatus.Running);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("saved state copies properties from context"), Fact]
    public async Task SavedStateCopiesProperties()
    {
        WorkflowState? captured = null;
        var store = MakeStore();
        store.SaveCheckpointAsync(Arg.Any<string>(), Arg.Any<WorkflowState>(), Arg.Any<CancellationToken>())
             .Returns(ci => { captured = ci.Arg<WorkflowState>(); return Task.CompletedTask; });

        var sut = new CheckpointMiddleware(store);
        var ctx = MakeContext();
        var step = Substitute.For<IStep>();

        await sut.InvokeAsync(ctx, step, _ => Task.CompletedTask);

        await Given("context has property 'key'", () => captured!)
            .Then("saved state contains 'key'", state =>
            {
                state.Properties.Should().ContainKey("key");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("checkpoint is not saved when next throws"), Fact]
    public async Task CheckpointNotSavedWhenNextThrows()
    {
        var store = MakeStore();
        var sut = new CheckpointMiddleware(store);
        var ctx = MakeContext();
        var step = Substitute.For<IStep>();

        try
        {
            await sut.InvokeAsync(ctx, step, _ => throw new InvalidOperationException("boom"));
        }
        catch (InvalidOperationException) { /* expected */ }

        await Given("next delegate threw an exception", () => store)
            .Then("SaveCheckpointAsync was never called", s =>
            {
                s.DidNotReceive().SaveCheckpointAsync(Arg.Any<string>(), Arg.Any<WorkflowState>(), Arg.Any<CancellationToken>());
                return true;
            })
            .AssertPassed();
    }
}
