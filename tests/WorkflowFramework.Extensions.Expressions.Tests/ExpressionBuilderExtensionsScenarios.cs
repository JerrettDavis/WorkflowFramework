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
}
