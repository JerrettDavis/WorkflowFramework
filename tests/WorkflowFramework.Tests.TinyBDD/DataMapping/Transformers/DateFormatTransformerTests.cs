using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.DataMapping.Transformers;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.DataMapping.Transformers;

[Feature("Date format transformer")]
public class DateFormatTransformerTests : TinyBddTestBase
{
    public DateFormatTransformerTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Reformats an ISO date to a custom output format"), Fact]
    public async Task ReformatsIsoDate() =>
        await Given("ISO date string 2024-03-15", () => "2024-03-15")
            .When("transform with outputFormat dd/MM/yyyy", input =>
                new DateFormatTransformer().Transform(input, new Dictionary<string, string?>
                {
                    ["outputFormat"] = "dd/MM/yyyy"
                }))
            .Then("output is 15/03/2024", result =>
            {
                result.Should().Be("15/03/2024");
            })
            .AssertPassed();

    [Scenario("Returns the input unchanged for an invalid date string"), Fact]
    public async Task ReturnsInputForInvalidDate() =>
        await Given("a non-date string", () => "not-a-date")
            .When("transform with dateFormat", input =>
                new DateFormatTransformer().Transform(input, new Dictionary<string, string?>
                {
                    ["outputFormat"] = "yyyy-MM-dd"
                }))
            .Then("result equals the original input", result =>
            {
                result.Should().Be("not-a-date");
            })
            .AssertPassed();

    [Scenario("Returns empty/null input as-is"), Fact]
    public async Task ReturnsNullForNullInput() =>
        await Given("null input", () => (string?)null)
            .When("transform null", input =>
                new DateFormatTransformer().Transform(input))
            .Then("result is null", result =>
            {
                result.Should().BeNull();
            })
            .AssertPassed();

    [Scenario("Uses default output format yyyy-MM-dd when no outputFormat arg provided"), Fact]
    public async Task UsesDefaultOutputFormat() =>
        await Given("ISO date string 2024-06-01", () => "2024-06-01")
            .When("transform with no args", input =>
                new DateFormatTransformer().Transform(input))
            .Then("output is 2024-06-01 (default format applied)", result =>
            {
                result.Should().Be("2024-06-01");
            })
            .AssertPassed();

    [Scenario("Parses with explicit inputFormat"), Fact]
    public async Task ParsesWithExplicitInputFormat() =>
        await Given("date string in MM-dd-yyyy format", () => "03-15-2024")
            .When("transform with inputFormat MM-dd-yyyy and outputFormat yyyy/MM/dd", input =>
                new DateFormatTransformer().Transform(input, new Dictionary<string, string?>
                {
                    ["inputFormat"] = "MM-dd-yyyy",
                    ["outputFormat"] = "yyyy/MM/dd"
                }))
            .Then("output is 2024/03/15", result =>
            {
                result.Should().Be("2024/03/15");
            })
            .AssertPassed();
}
