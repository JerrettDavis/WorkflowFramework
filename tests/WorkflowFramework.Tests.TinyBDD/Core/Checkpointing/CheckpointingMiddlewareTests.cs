using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Checkpointing;
using WorkflowFramework.Tests.TinyBDD.Support;

namespace WorkflowFramework.Tests.TinyBDD.Core.Checkpointing;

[Feature("CheckpointingMiddleware behaviour")]
public class CheckpointingMiddlewareTests : TinyBddTestBase
{
    public CheckpointingMiddlewareTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Middleware saves a checkpoint after every successful step"), Fact]
    public async Task SavesCheckpointAfterSuccessfulStep()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var workflow = Workflow.Create("cp-test")
            .Step(new Testing.LambdaStep("step-1", ctx => { ctx.Properties["s1"] = true; return Task.CompletedTask; }))
            .Step(new Testing.LambdaStep("step-2", ctx => { ctx.Properties["s2"] = true; return Task.CompletedTask; }))
            .Use(new CheckpointingMiddleware(store))
            .Build();

        var ctx = new WorkflowContext();
        await workflow.ExecuteAsync(ctx);
        var cp = await store.LoadAsync(ctx.WorkflowId);

        await Given("a successful two-step workflow with checkpointing", () => cp)
            .Then("a checkpoint was saved", saved =>
            {
                saved.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Middleware does NOT save a checkpoint when a step throws"), Fact]
    public async Task DoesNotSaveCheckpointWhenStepFails()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var workflow = Workflow.Create("cp-fail")
            .Step(new Testing.LambdaStep("boom", _ => throw new InvalidOperationException("step failure")))
            .Use(new CheckpointingMiddleware(store))
            .Build();

        var ctx = new WorkflowContext();
        await workflow.ExecuteAsync(ctx);
        var cp = await store.LoadAsync(ctx.WorkflowId);

        await Given("a workflow that faults on its only step", () => cp)
            .Then("no checkpoint is written", saved =>
            {
                saved.Should().BeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Builder extension WithCheckpointing registers the middleware"), Fact]
    public async Task WithCheckpointingExtensionRegistersMiddleware()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var workflow = Workflow.Create("cp-ext")
            .Step(new Testing.LambdaStep("step-a", _ => Task.CompletedTask))
            .WithCheckpointing(store)
            .Build();

        var ctx = new WorkflowContext();
        var result = await workflow.ExecuteAsync(ctx);
        var cp = await store.LoadAsync(ctx.WorkflowId);

        await Given("a workflow built with WithCheckpointing", () => (result, cp))
            .Then("execution succeeds and the store contains a checkpoint", pair =>
            {
                pair.result.IsSuccess.Should().BeTrue();
                pair.cp.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }
}
