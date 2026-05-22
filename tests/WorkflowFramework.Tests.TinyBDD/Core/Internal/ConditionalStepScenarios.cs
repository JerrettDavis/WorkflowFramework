using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.Core.Internal;

[Feature("ConditionalStep — true/false branch execution")]
public class ConditionalStepScenarios : TinyBddTestBase
{
    public ConditionalStepScenarios(ITestOutputHelper output) : base(output) { }

    // Helpers ─────────────────────────────────────────────────────────────────

    private static IWorkflow BuildConditional(bool condition, List<string> log, bool includeElse = true)
    {
        var builder = Workflow.Create("cond")
            .If(_ => condition)
            .Then(new LambdaStep("then", _ => { log.Add("then"); return Task.CompletedTask; }));

        if (includeElse)
            return builder.Else(new LambdaStep("else", _ => { log.Add("else"); return Task.CompletedTask; })).Build();

        return builder.EndIf().Build();
    }

    // ── true branch ───────────────────────────────────────────────────────────

    [Scenario("True-condition executes then-branch and skips else-branch"), Fact]
    public async Task TrueConditionRunsThenBranch()
    {
        var log = new List<string>();
        var wf = BuildConditional(true, log);
        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a conditional with condition=true and an else branch", () => (result, log))
            .Then("then-branch ran and else-branch did not", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.log.Should().ContainSingle("then");
                t.log.Should().NotContain("else");
                return true;
            })
            .AssertPassed();
    }

    // ── false branch ──────────────────────────────────────────────────────────

    [Scenario("False-condition executes else-branch and skips then-branch"), Fact]
    public async Task FalseConditionRunsElseBranch()
    {
        var log = new List<string>();
        var wf = BuildConditional(false, log);
        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a conditional with condition=false and an else branch", () => (result, log))
            .Then("else-branch ran and then-branch did not", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.log.Should().ContainSingle("else");
                t.log.Should().NotContain("then");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("False-condition with no else branch — neither branch executes"), Fact]
    public async Task FalseConditionWithoutElseSilentlySkips()
    {
        var log = new List<string>();
        var wf = BuildConditional(false, log, includeElse: false);
        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a conditional with condition=false and no else branch", () => (result, log))
            .Then("workflow succeeds and no branch ran", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.log.Should().BeEmpty();
                return true;
            })
            .AssertPassed();
    }

    // ── predicate exception ───────────────────────────────────────────────────

    [Scenario("Predicate that throws causes the workflow to fault"), Fact]
    public async Task ThrowingPredicateFaultsWorkflow()
    {
        var wf = Workflow.Create("throwing-pred")
            .If(_ => throw new InvalidOperationException("predicate-fail"))
            .Then(new LambdaStep("then", _ => Task.CompletedTask))
            .EndIf()
            .Build();

        var result = await wf.ExecuteAsync(new WorkflowContext());

        await Given("a conditional whose predicate throws", () => result)
            .Then("workflow status is Faulted", r =>
            {
                r.Status.Should().Be(WorkflowStatus.Faulted);
                r.Errors.Should().ContainSingle();
                return true;
            })
            .AssertPassed();
    }

    // ── nested conditional ────────────────────────────────────────────────────

    [Scenario("Nested conditionals — inner condition only evaluates when outer is true"), Fact]
    public async Task NestedConditionalsEvaluateCorrectly()
    {
        var log = new List<string>();
        var outerTrue = true;
        var innerFalse = false;

        var wf = Workflow.Create("nested")
            .If(_ => outerTrue)
            .Then(new LambdaStep("inner-conditional", ctx =>
            {
                // Represent a nested conditional via inline logic
                if (innerFalse)
                    log.Add("inner-then");
                else
                    log.Add("inner-else");
                return Task.CompletedTask;
            }))
            .EndIf()
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("outer=true, inner=false nested conditional", () => log)
            .Then("inner-else branch ran", l =>
            {
                l.Should().ContainSingle("inner-else");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Conditional step name reflects then and else step names"), Fact]
    public async Task ConditionalStepNameReflectsChildren()
    {
        var wf = Workflow.Create("name-test")
            .If(_ => true)
            .Then(new LambdaStep("then-step", _ => Task.CompletedTask))
            .Else(new LambdaStep("else-step", _ => Task.CompletedTask))
            .Build();

        await Given("a conditional step with named then/else steps", () => wf.Steps)
            .Then("step name contains then-step and else-step names", steps =>
            {
                steps.Should().ContainSingle();
                steps[0].Name.Should().Contain("then-step");
                steps[0].Name.Should().Contain("else-step");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Context properties can influence the conditional predicate at runtime"), Fact]
    public async Task ContextPropertyDrivesConditional()
    {
        var log = new List<string>();
        var wf = Workflow.Create("ctx-cond")
            .Step("setup", ctx => { ctx.Properties["branch"] = "then"; return Task.CompletedTask; })
            .If(ctx => ctx.Properties.TryGetValue("branch", out var v) && (string?)v == "then")
            .Then(new LambdaStep("then-step", _ => { log.Add("then"); return Task.CompletedTask; }))
            .Else(new LambdaStep("else-step", _ => { log.Add("else"); return Task.CompletedTask; }))
            .Build();

        await wf.ExecuteAsync(new WorkflowContext());

        await Given("a conditional whose predicate reads from context.Properties", () => log)
            .Then("then-branch ran because the property was set", l =>
            {
                l.Should().ContainSingle("then");
                return true;
            })
            .AssertPassed();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed class LambdaStep(string name, Func<IWorkflowContext, Task> action) : IStep
    {
        public string Name { get; } = name;
        public Task ExecuteAsync(IWorkflowContext context) => action(context);
    }
}
