using FluentAssertions;
using WorkflowFramework.Extensions.Expressions;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Expressions;

public class SimpleExpressionEvaluatorTests
{
    private readonly SimpleExpressionEvaluator _eval = new();
    private readonly Dictionary<string, object?> _vars = new();

    [Fact]
    public void Name_IsSimple() => _eval.Name.Should().Be("simple");

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    public async Task BooleanLiterals(string expr, bool expected)
    {
        var result = await _eval.EvaluateAsync<bool>(expr, _vars);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task NullLiteral()
    {
        var result = await _eval.EvaluateAsync("null", _vars);
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("42", 42.0)]
    [InlineData("3.14", 3.14)]
    [InlineData("-1", -1.0)]
    public async Task NumericLiterals(string expr, double expected)
    {
        var result = await _eval.EvaluateAsync<double>(expr, _vars);
        result.Should().BeApproximately(expected, 0.001);
    }

    [Theory]
    [InlineData("'hello'", "hello")]
    [InlineData("\"world\"", "world")]
    public async Task StringLiterals(string expr, string expected)
    {
        var result = await _eval.EvaluateAsync<string>(expr, _vars);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task VariableLookup()
    {
        _vars["x"] = 42.0;
        var result = await _eval.EvaluateAsync<double>("x", _vars);
        result.Should().Be(42.0);
    }

    [Fact]
    public async Task UnknownVariable_Throws()
    {
        await _eval.Invoking(e => e.EvaluateAsync("unknown", _vars))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*Cannot evaluate*");
    }

    [Theory]
    [InlineData("10 == 10", true)]
    [InlineData("10 != 5", true)]
    [InlineData("10 > 5", true)]
    [InlineData("5 < 10", true)]
    [InlineData("10 >= 10", true)]
    [InlineData("10 <= 10", true)]
    [InlineData("5 > 10", false)]
    public async Task Comparisons(string expr, bool expected)
    {
        var result = await _eval.EvaluateAsync<bool>(expr, _vars);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task NullComparison_BothNull_Equal()
    {
        _vars["a"] = null;
        _vars["b"] = null;
        var result = await _eval.EvaluateAsync<bool>("null == null", _vars);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task NullComparison_OneNull_NotEqual()
    {
        var result = await _eval.EvaluateAsync<bool>("null != 5", _vars);
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("true && true", true)]
    [InlineData("true && false", false)]
    [InlineData("true || false", true)]
    [InlineData("false || false", false)]
    public async Task BooleanLogic(string expr, bool expected)
    {
        var result = await _eval.EvaluateAsync<bool>(expr, _vars);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Arithmetic_Addition()
    {
        _vars["x"] = 10.0;
        var result = await _eval.EvaluateAsync<double>("x + 5", _vars);
        result.Should().Be(15.0);
    }

    [Fact]
    public async Task Arithmetic_Subtraction()
    {
        _vars["x"] = 10.0;
        var result = await _eval.EvaluateAsync<double>("x - 3", _vars);
        result.Should().Be(7.0);
    }

    [Fact]
    public async Task Arithmetic_Multiplication()
    {
        var result = await _eval.EvaluateAsync<double>("3 * 4", _vars);
        result.Should().Be(12.0);
    }

    [Fact]
    public async Task Arithmetic_Division()
    {
        var result = await _eval.EvaluateAsync<double>("10 / 2", _vars);
        result.Should().Be(5.0);
    }

    [Fact]
    public async Task Arithmetic_DivideByZero_Throws()
    {
        await _eval.Invoking(e => e.EvaluateAsync<double>("10 / 0", _vars))
            .Should().ThrowAsync<DivideByZeroException>();
    }

    [Fact]
    public async Task EvaluateAsync_ObjectOverload_ReturnsObject()
    {
        var result = await _eval.EvaluateAsync("42", _vars);
        result.Should().Be(42.0);
    }

    [Fact]
    public async Task EvaluateAsync_NullResult_ReturnsDefault()
    {
        var result = await _eval.EvaluateAsync<string>("null", _vars);
        result.Should().BeNull();
    }
}
