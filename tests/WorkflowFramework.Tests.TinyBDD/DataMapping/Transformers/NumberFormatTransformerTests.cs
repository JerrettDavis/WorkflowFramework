using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.DataMapping.Transformers;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.DataMapping.Transformers;

[Feature("Number format transformer")]
public class NumberFormatTransformerTests : TinyBddTestBase
{
    public NumberFormatTransformerTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Formats a decimal to N2 (default) when no format arg given"), Fact]
    public async Task FormatsWithDefaultN2() =>
        await Given("numeric string '1234.5'", () => "1234.5")
            .When("transform with no args", input =>
                new NumberFormatTransformer().Transform(input))
            .Then("result uses N2 format '1,234.50'", result =>
            {
                result.Should().Be("1,234.50");
            })
            .AssertPassed();

    [Scenario("Formats a number with a custom format N0"), Fact]
    public async Task FormatsWithCustomN0() =>
        await Given("numeric string '9876.543'", () => "9876.543")
            .When("transform with format N0", input =>
                new NumberFormatTransformer().Transform(input, new Dictionary<string, string?>
                {
                    ["format"] = "N0"
                }))
            .Then("result is '9,877' (rounded integer)", result =>
            {
                result.Should().Be("9,877");
            })
            .AssertPassed();

    [Scenario("Returns input unchanged for a non-numeric string"), Fact]
    public async Task ReturnsInputForNonNumeric() =>
        await Given("non-numeric string 'abc'", () => "abc")
            .When("transform with format N2", input =>
                new NumberFormatTransformer().Transform(input, new Dictionary<string, string?>
                {
                    ["format"] = "N2"
                }))
            .Then("result is still 'abc'", result =>
            {
                result.Should().Be("abc");
            })
            .AssertPassed();

    [Scenario("Returns null input as-is"), Fact]
    public async Task ReturnsNullForNullInput() =>
        await Given("null input", () => (string?)null)
            .When("transform null", input =>
                new NumberFormatTransformer().Transform(input))
            .Then("result is null", result =>
            {
                result.Should().BeNull();
            })
            .AssertPassed();
}
