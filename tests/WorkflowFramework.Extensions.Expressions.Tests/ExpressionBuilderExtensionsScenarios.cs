using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Expressions.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Expressions.Tests;

[Feature("ExpressionBuilderExtensions")]
public class ExpressionBuilderExtensionsScenarios : ExpressionsTestBase
{
    public ExpressionBuilderExtensionsScenarios(ITestOutputHelper output) : base(output) { }

    // ---- helper steps ----

    private sealed class FlagStep(string name, Action<string> record) : IStep
    {
        public string Name => name;
        public Task ExecuteAsync(IWorkflowContext context)
        {
            record(name);
            return Task.CompletedTask;
        }
    }

    [Scenario("IfExpression routes to then-branch when expression evaluates to true"), Fact]
    public async Task IfExpressionTakeThenBranch()
    {
        var executed = new List<string>();

        var workflow = Workflow.Create("if-expr-true")
            .IfExpression("true")
                .Then(new FlagStep("then-step", executed.Add))
                .Else(new FlagStep("else-step", executed.Add))
            .Step("end", _ => Task.CompletedTask)
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        await Given("IfExpression('true')", () => (result, executed))
            .Then("then-branch runs, else-branch skipped", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.executed.Should().ContainSingle().Which.Should().Be("then-step");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("IfExpression routes to else-branch when expression evaluates to false"), Fact]
    public async Task IfExpressionTakeElseBranch()
    {
        var executed = new List<string>();

        var workflow = Workflow.Create("if-expr-false")
            .IfExpression("false")
                .Then(new FlagStep("then-step", executed.Add))
                .Else(new FlagStep("else-step", executed.Add))
            .Step("end", _ => Task.CompletedTask)
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        await Given("IfExpression('false')", () => (result, executed))
            .Then("else-branch runs, then-branch skipped", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.executed.Should().ContainSingle().Which.Should().Be("else-step");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("IfExpression uses variable in context Properties for routing"), Fact]
    public async Task IfExpressionUsesContextProperties()
    {
        var executed = new List<string>();

        var workflow = Workflow.Create("if-expr-var")
            .Step("set-flag", ctx =>
            {
                ctx.Properties["flag"] = true;
                return Task.CompletedTask;
            })
            .IfExpression("flag")
                .Then(new FlagStep("then-step", executed.Add))
                .Else(new FlagStep("else-step", executed.Add))
            .Step("end", _ => Task.CompletedTask)
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        await Given("IfExpression using a flag stored in context properties", () => (result, executed))
            .Then("then-branch runs when flag is true", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.executed.Should().ContainSingle().Which.Should().Be("then-step");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("IfExpression with a custom evaluator uses the provided evaluator"), Fact]
    public async Task IfExpressionUsesCustomEvaluator()
    {
        var executed = new List<string>();
        // Evaluator that always returns false regardless of expression
        var alwaysFalse = new AlwaysFalseEvaluator();

        var workflow = Workflow.Create("if-expr-custom-eval")
            .IfExpression("true", alwaysFalse) // expression says true, evaluator returns false
                .Then(new FlagStep("then-step", executed.Add))
                .Else(new FlagStep("else-step", executed.Add))
            .Step("end", _ => Task.CompletedTask)
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        await Given("IfExpression('true') with an always-false evaluator", () => (result, executed))
            .Then("else-branch runs because the custom evaluator overrides", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.executed.Should().ContainSingle().Which.Should().Be("else-step");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("IfExpression skips else-branch when expression is true and no else is defined"), Fact]
    public async Task IfExpressionNoElseBranch()
    {
        var executed = new List<string>();

        // IElseBuilder.EndIf() returns the parent IWorkflowBuilder for chaining
        var workflow = Workflow.Create("if-expr-no-else")
            .IfExpression("true")
                .Then(new FlagStep("then-step", executed.Add))
                .EndIf()
            .Step("end", _ => { executed.Add("end"); return Task.CompletedTask; })
            .Build();

        var context = new WorkflowContext();
        var result = await workflow.ExecuteAsync(context);

        await Given("IfExpression('true') without an else branch, using EndIf()", () => (result, executed))
            .Then("then and end steps both run", t =>
            {
                t.result.IsSuccess.Should().BeTrue();
                t.executed.Should().Equal("then-step", "end");
                return true;
            })
            .AssertPassed();
    }

    // --- helper evaluators ---

    private sealed class AlwaysFalseEvaluator : IExpressionEvaluator
    {
        public string Name => "always-false";

        public Task<T?> EvaluateAsync<T>(string expression, IDictionary<string, object?> variables, CancellationToken ct = default)
        {
            // Return false for bool, default for anything else
            object? val = typeof(T) == typeof(bool) ? (object?)false : default(T);
            return Task.FromResult((T?)val);
        }

        public Task<object?> EvaluateAsync(string expression, IDictionary<string, object?> variables, CancellationToken ct = default)
            => Task.FromResult<object?>(false);
    }
}
