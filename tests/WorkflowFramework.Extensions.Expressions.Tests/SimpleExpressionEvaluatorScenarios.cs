using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Expressions.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Expressions.Tests;

[Feature("SimpleExpressionEvaluator")]
public class SimpleExpressionEvaluatorScenarios : ExpressionsTestBase
{
    public SimpleExpressionEvaluatorScenarios(ITestOutputHelper output) : base(output) { }

    private static readonly SimpleExpressionEvaluator Evaluator = new();
    private static readonly Dictionary<string, object?> EmptyVars = new();

    [Scenario("Evaluates boolean literal true"), Fact]
    public async Task EvalBooleanTrue()
    {
        var result = await Evaluator.EvaluateAsync<bool>("true", EmptyVars);

        await Given("expression 'true'", () => result)
            .Then("evaluates to true", v => { v.Should().BeTrue(); return true; })
            .AssertPassed();
    }

    [Scenario("Evaluates boolean literal false"), Fact]
    public async Task EvalBooleanFalse()
    {
        var result = await Evaluator.EvaluateAsync<bool>("false", EmptyVars);

        await Given("expression 'false'", () => result)
            .Then("evaluates to false", v => { v.Should().BeFalse(); return true; })
            .AssertPassed();
    }

    [Scenario("Evaluates a numeric literal"), Fact]
    public async Task EvalNumericLiteral()
    {
        var result = await Evaluator.EvaluateAsync<double>("42", EmptyVars);

        await Given("expression '42'", () => result)
            .Then("evaluates to 42", v => { v.Should().Be(42); return true; })
            .AssertPassed();
    }

    [Scenario("Evaluates simple arithmetic addition"), Fact]
    public async Task EvalAddition()
    {
        var result = await Evaluator.EvaluateAsync<double>("3 + 4", EmptyVars);

        await Given("expression '3 + 4'", () => result)
            .Then("evaluates to 7", v => { v.Should().Be(7); return true; })
            .AssertPassed();
    }

    [Scenario("Resolves a variable from the dictionary"), Fact]
    public async Task EvalVariableLookup()
    {
        var vars = new Dictionary<string, object?> { ["x"] = 99.0 };
        var result = await Evaluator.EvaluateAsync<double>("x", vars);

        await Given("variable x=99", () => result)
            .Then("evaluates to 99", v => { v.Should().Be(99); return true; })
            .AssertPassed();
    }

    [Scenario("Throws when variable is not found"), Fact]
    public async Task EvalMissingVariableThrows()
    {
        Func<Task> act = () => Evaluator.EvaluateAsync<double>("unknown", EmptyVars);

        await Given("expression referencing an undefined variable", () => act)
            .Then("throws InvalidOperationException", fn =>
            {
                fn.Should().ThrowAsync<InvalidOperationException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Evaluates equality comparison that is true"), Fact]
    public async Task EvalEqualityTrue()
    {
        var vars = new Dictionary<string, object?> { ["n"] = 5.0 };
        var result = await Evaluator.EvaluateAsync<bool>("n == 5", vars);

        await Given("n==5 where n=5", () => result)
            .Then("evaluates to true", v => { v.Should().BeTrue(); return true; })
            .AssertPassed();
    }

    [Scenario("Evaluates a string literal"), Fact]
    public async Task EvalStringLiteral()
    {
        var result = await Evaluator.EvaluateAsync<string>("'hello'", EmptyVars);

        await Given("expression 'hello'", () => result)
            .Then("evaluates to hello", v => { v.Should().Be("hello"); return true; })
            .AssertPassed();
    }
}
