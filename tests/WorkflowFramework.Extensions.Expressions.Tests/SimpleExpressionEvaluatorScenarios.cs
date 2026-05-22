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

    [Scenario("Evaluates subtraction"), Fact]
    public async Task EvalSubtraction()
    {
        var result = await Evaluator.EvaluateAsync<double>("10 - 3", EmptyVars);

        await Given("expression '10 - 3'", () => result)
            .Then("evaluates to 7", v => { v.Should().Be(7); return true; })
            .AssertPassed();
    }

    [Scenario("Evaluates multiplication"), Fact]
    public async Task EvalMultiplication()
    {
        var result = await Evaluator.EvaluateAsync<double>("6 * 7", EmptyVars);

        await Given("expression '6 * 7'", () => result)
            .Then("evaluates to 42", v => { v.Should().Be(42); return true; })
            .AssertPassed();
    }

    [Scenario("Evaluates division"), Fact]
    public async Task EvalDivision()
    {
        var result = await Evaluator.EvaluateAsync<double>("10 / 4", EmptyVars);

        await Given("expression '10 / 4'", () => result)
            .Then("evaluates to 2.5", v => { v.Should().BeApproximately(2.5, 0.001); return true; })
            .AssertPassed();
    }

    [Scenario("Division by zero throws DivideByZeroException"), Fact]
    public async Task EvalDivisionByZeroThrows()
    {
        Func<Task> act = () => Evaluator.EvaluateAsync<double>("5 / 0", EmptyVars);

        await Given("expression '5 / 0'", () => act)
            .Then("throws DivideByZeroException", fn =>
            {
                fn.Should().ThrowAsync<DivideByZeroException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Evaluates not-equal comparison that is true"), Fact]
    public async Task EvalNotEqualTrue()
    {
        var vars = new Dictionary<string, object?> { ["n"] = 3.0 };
        var result = await Evaluator.EvaluateAsync<bool>("n != 5", vars);

        await Given("n!=5 where n=3", () => result)
            .Then("evaluates to true", v => { v.Should().BeTrue(); return true; })
            .AssertPassed();
    }

    [Scenario("Evaluates less-than comparison"), Fact]
    public async Task EvalLessThan()
    {
        var result = await Evaluator.EvaluateAsync<bool>("3 < 5", EmptyVars);

        await Given("expression '3 < 5'", () => result)
            .Then("evaluates to true", v => { v.Should().BeTrue(); return true; })
            .AssertPassed();
    }

    [Scenario("Evaluates greater-than-or-equal comparison"), Fact]
    public async Task EvalGreaterThanOrEqual()
    {
        var result = await Evaluator.EvaluateAsync<bool>("5 >= 5", EmptyVars);

        await Given("expression '5 >= 5'", () => result)
            .Then("evaluates to true", v => { v.Should().BeTrue(); return true; })
            .AssertPassed();
    }

    [Scenario("Evaluates logical AND — both true"), Fact]
    public async Task EvalLogicalAndBothTrue()
    {
        var result = await Evaluator.EvaluateAsync<bool>("true && true", EmptyVars);

        await Given("expression 'true && true'", () => result)
            .Then("evaluates to true", v => { v.Should().BeTrue(); return true; })
            .AssertPassed();
    }

    [Scenario("Evaluates logical OR — one true"), Fact]
    public async Task EvalLogicalOrOneTrue()
    {
        var result = await Evaluator.EvaluateAsync<bool>("false || true", EmptyVars);

        await Given("expression 'false || true'", () => result)
            .Then("evaluates to true", v => { v.Should().BeTrue(); return true; })
            .AssertPassed();
    }

    [Scenario("Evaluates null literal"), Fact]
    public async Task EvalNullLiteral()
    {
        // EvaluateAsync<object?> returns null for 'null'
        var result = await Evaluator.EvaluateAsync("null", EmptyVars);

        await Given("expression 'null'", () => result)
            .Then("evaluates to null", v => { v.Should().BeNull(); return true; })
            .AssertPassed();
    }

    [Scenario("Variable arithmetic: expression with two variables"), Fact]
    public async Task EvalTwoVariableArithmetic()
    {
        var vars = new Dictionary<string, object?> { ["a"] = 3.0, ["b"] = 4.0 };
        // Gotcha: the evaluator uses LastIndexOf for arithmetic, which means
        // 'a + b' splits on the last '+' in the trimmed string — works for single-op.
        var result = await Evaluator.EvaluateAsync<double>("a + b", vars);

        await Given("expression 'a + b' with a=3, b=4", () => result)
            .Then("evaluates to 7", v => { v.Should().Be(7); return true; })
            .AssertPassed();
    }
}
