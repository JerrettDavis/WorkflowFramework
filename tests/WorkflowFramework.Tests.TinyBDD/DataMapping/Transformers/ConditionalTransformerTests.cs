using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.DataMapping.Transformers;
using WorkflowFramework.Tests.TinyBDD.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Tests.TinyBDD.DataMapping.Transformers;

[Feature("Conditional transformer")]
public class ConditionalTransformerTests : TinyBddTestBase
{
    public ConditionalTransformerTests(ITestOutputHelper output) : base(output) { }

    [Scenario("Returns 'then' value when input matches 'equals'"), Fact]
    public async Task ReturnsThenOnMatch() =>
        await Given("input 'active'", () => "active")
            .When("transform with equals=active then=ACTIVE", input =>
                new ConditionalTransformer().Transform(input, new Dictionary<string, string?>
                {
                    ["equals"] = "active",
                    ["then"] = "ACTIVE",
                    ["else"] = "INACTIVE"
                }))
            .Then("result is ACTIVE", result =>
            {
                result.Should().Be("ACTIVE");
            })
            .AssertPassed();

    [Scenario("Returns 'else' value when input does not match"), Fact]
    public async Task ReturnsElseOnNonMatch() =>
        await Given("input 'inactive'", () => "inactive")
            .When("transform with equals=active then=yes else=no", input =>
                new ConditionalTransformer().Transform(input, new Dictionary<string, string?>
                {
                    ["equals"] = "active",
                    ["then"] = "yes",
                    ["else"] = "no"
                }))
            .Then("result is no", result =>
            {
                result.Should().Be("no");
            })
            .AssertPassed();

    [Scenario("Comparison is case-insensitive"), Fact]
    public async Task ComparisonIsCaseInsensitive() =>
        await Given("input 'ACTIVE' (upper case)", () => "ACTIVE")
            .When("transform with equals=active (lower case)", input =>
                new ConditionalTransformer().Transform(input, new Dictionary<string, string?>
                {
                    ["equals"] = "active",
                    ["then"] = "matched",
                    ["else"] = "not-matched"
                }))
            .Then("result is matched (case insensitive)", result =>
            {
                result.Should().Be("matched");
            })
            .AssertPassed();

    [Scenario("Returns input unchanged when args is null"), Fact]
    public async Task ReturnsInputWhenArgsNull() =>
        await Given("input 'value'", () => "value")
            .When("transform with null args", input =>
                new ConditionalTransformer().Transform(input, null))
            .Then("result is the original input", result =>
            {
                result.Should().Be("value");
            })
            .AssertPassed();

    [Scenario("Returns input as fallback when then arg is absent"), Fact]
    public async Task ReturnsInputAsFallbackWhenThenAbsent() =>
        await Given("input 'yes'", () => "yes")
            .When("transform with equals=yes but no then value", input =>
                new ConditionalTransformer().Transform(input, new Dictionary<string, string?>
                {
                    ["equals"] = "yes"
                }))
            .Then("result is the original input (no then defined)", result =>
            {
                result.Should().Be("yes");
            })
            .AssertPassed();
}
