using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Diagnostics.ExecutionHistory;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Diagnostics.Tests.Diagnostics;

[Feature("ExecutionHistoryBuilderExtensions — typed and untyped workflow history registration")]
public class ExecutionHistoryBuilderExtensionsScenarios : TinyBddXunitBase
{
    public ExecutionHistoryBuilderExtensionsScenarios(ITestOutputHelper output) : base(output) { }

    private sealed class NoopStep(string name) : IStep
    {
        public string Name => name;
        public Task ExecuteAsync(IWorkflowContext context) => Task.CompletedTask;
    }

    // ── untyped builder ───────────────────────────────────────────────────

    [Scenario("WithExecutionHistory(store) adds history tracking to untyped workflow"), Fact]
    public async Task UntypedWithStore_RecordsHistory()
    {
        var store = new InMemoryExecutionHistoryStore();
        var wf = Workflow.Create("history-untyped")
            .Step(new NoopStep("step1"))
            .WithExecutionHistory(store)
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("execution history store after running an untyped workflow", () => store)
            .Then("one record was persisted with a non-empty RunId", s =>
            {
                s.AllRecords.Should().HaveCount(1);
                s.AllRecords[0].RunId.Should().NotBeNullOrEmpty();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WithExecutionHistory(out store) creates in-memory store and records history"), Fact]
    public async Task UntypedWithOutStore_CreatesAndRecords()
    {
        var wf = Workflow.Create("history-out")
            .Step(new NoopStep("step1"))
            .WithExecutionHistory(out var store)
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("auto-created in-memory store", () => store)
            .Then("one record exists in the auto-created store", s =>
            {
                s.AllRecords.Should().HaveCount(1);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WithExecutionHistory null store on untyped builder throws ArgumentNullException"), Fact]
    public async Task UntypedNullStore_Throws()
    {
        Exception? caught = null;
        try
        {
            Workflow.Create("null-store")
                .Step(new NoopStep("step1"))
                .WithExecutionHistory(null!);
        }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null store passed to untyped builder", () => caught)
            .Then("ArgumentNullException is thrown with paramName store", ex =>
            {
                ex.Should().NotBeNull();
                ((ArgumentNullException)ex!).ParamName.Should().Be("store");
                return true;
            })
            .AssertPassed();
    }

    // ── typed builder ─────────────────────────────────────────────────────

    [Scenario("WithExecutionHistory<TData>(store) adds history tracking to typed workflow"), Fact]
    public async Task TypedWithStore_RecordsHistory()
    {
        var store = new InMemoryExecutionHistoryStore();
        var wf = Workflow.Create<HistData>("history-typed")
            .Step("step1", ctx => { ctx.Data.Value = 42; return Task.CompletedTask; })
            .WithExecutionHistory(store)
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext<HistData>(new HistData()));

        await Given("execution history store after running a typed workflow", () => (store, result))
            .Then("one record was persisted and result is successful", t =>
            {
                t.store.AllRecords.Should().HaveCount(1);
                t.store.AllRecords[0].RunId.Should().NotBeNullOrEmpty();
                t.result.IsSuccess.Should().BeTrue();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("WithExecutionHistory<TData> null store on typed builder throws ArgumentNullException"), Fact]
    public async Task TypedNullStore_Throws()
    {
        Exception? caught = null;
        try
        {
            Workflow.Create<HistData>("null-typed")
                .Step("step1", _ => Task.CompletedTask)
                .WithExecutionHistory<HistData>((IExecutionHistoryStore)null!);
        }
        catch (ArgumentNullException ex) { caught = ex; }

        await Given("null store passed to typed builder", () => caught)
            .Then("ArgumentNullException is thrown with paramName store", ex =>
            {
                ex.Should().NotBeNull();
                ((ArgumentNullException)ex!).ParamName.Should().Be("store");
                return true;
            })
            .AssertPassed();
    }
}

file sealed class HistData { public int Value { get; set; } }
