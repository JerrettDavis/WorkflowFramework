using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using WorkflowFramework.Checkpointing;
using WorkflowFramework.Tests.TinyBDD.Support;

namespace WorkflowFramework.Tests.TinyBDD.Core.Checkpointing;

[Feature("CheckpointingBuilderExtensions")]
public class CheckpointingBuilderExtensionsTests : TinyBddTestBase
{
    public CheckpointingBuilderExtensionsTests(ITestOutputHelper output) : base(output) { }

    [Scenario("WithCheckpointing on untyped builder adds middleware to the pipeline"), Fact]
    public async Task UntypedBuilderWithCheckpointingAddsMiddleware()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var workflow = Workflow.Create("ext-untyped")
            .Step(new Testing.LambdaStep("s1", _ => Task.CompletedTask))
            .WithCheckpointing(store)
            .Build();

        var ctx = new WorkflowContext();
        var result = await workflow.ExecuteAsync(ctx);
        var cp = await store.LoadAsync(ctx.WorkflowId);

        await Given("an untyped workflow built with WithCheckpointing", () => (result, cp))
            .Then("execution succeeds and checkpoint was saved", pair =>
            {
                pair.result.IsSuccess.Should().BeTrue();
                pair.cp.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WithCheckpointing on typed builder adds middleware to the pipeline"), Fact]
    public async Task TypedBuilderWithCheckpointingAddsMiddleware()
    {
        var store = new InMemoryWorkflowCheckpointStore();
        var workflow = Workflow.Create<BuildExtPayload>("ext-typed")
            .Step("s1", ctx => { ctx.Data.Value = 42; return Task.CompletedTask; })
            .WithCheckpointing(store)
            .Build();

        var ctx = new WorkflowContext<BuildExtPayload>(new BuildExtPayload());
        var result = await workflow.ExecuteAsync(ctx);
        var cp = await store.LoadAsync(ctx.WorkflowId);

        await Given("a typed workflow built with WithCheckpointing", () => (result, cp, ctx.Data.Value))
            .Then("execution succeeds, data is mutated and checkpoint was saved", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.Value.Should().Be(42);
                t.cp.Should().NotBeNull();
                return true;
            })
            .AssertPassed();
    }
}

file sealed class BuildExtPayload { public int Value { get; set; } }
