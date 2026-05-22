using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.DataMapping.Transformers;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.DataMapping.Transformers;

[Feature("Regex replace transformer")]
public class RegexReplaceTransformerTests : TinyBddTestBase
{
    public RegexReplaceTransformerTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Replaces matched pattern with replacement string"), Fact]
    public async Task ReplacesMatchedPattern() =>
        await Given("input string 'hello world'", () => "hello world")
            .When("replace 'world' with 'there'", input =>
                new RegexReplaceTransformer().Transform(input, new Dictionary<string, string?>
                {
                    ["pattern"] = "world",
                    ["replacement"] = "there"
                }))
            .Then("result is 'hello there'", result =>
            {
                result.Should().Be("hello there");
            })
            .AssertPassed();

    [Scenario("Returns input unchanged when pattern does not match"), Fact]
    public async Task ReturnsInputWhenNoMatch() =>
        await Given("input string 'abc'", () => "abc")
            .When("apply pattern that won't match", input =>
                new RegexReplaceTransformer().Transform(input, new Dictionary<string, string?>
                {
                    ["pattern"] = "xyz",
                    ["replacement"] = "replaced"
                }))
            .Then("result is still 'abc'", result =>
            {
                result.Should().Be("abc");
            })
            .AssertPassed();

    [Scenario("Returns input unchanged when args is null"), Fact]
    public async Task ReturnsInputWhenArgsNull() =>
        await Given("input string 'data'", () => "data")
            .When("transform with null args", input =>
                new RegexReplaceTransformer().Transform(input, null))
            .Then("result is 'data'", result =>
            {
                result.Should().Be("data");
            })
            .AssertPassed();

    [Scenario("Removes matched text when replacement is empty"), Fact]
    public async Task RemovesMatchedText() =>
        await Given("input string 'hello123world'", () => "hello123world")
            .When("replace digits with empty string", input =>
                new RegexReplaceTransformer().Transform(input, new Dictionary<string, string?>
                {
                    ["pattern"] = @"\d+",
                    ["replacement"] = ""
                }))
            .Then("result is 'helloworld'", result =>
            {
                result.Should().Be("helloworld");
            })
            .AssertPassed();

    [Scenario("Returns null input as-is"), Fact]
    public async Task ReturnsNullForNullInput() =>
        await Given("null input", () => (string?)null)
            .When("transform null", input =>
                new RegexReplaceTransformer().Transform(input, new Dictionary<string, string?>
                {
                    ["pattern"] = "x",
                    ["replacement"] = "y"
                }))
            .Then("result is null", result =>
            {
                result.Should().BeNull();
            })
            .AssertPassed();
}
