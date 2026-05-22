using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.AI;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.AI.Tests.AI;

[Feature("PromptTemplateRenderer — substitutes tokens in prompt strings")]
public class PromptTemplateRendererScenarios : TinyBddXunitBase
{
    public PromptTemplateRendererScenarios(ITestOutputHelper output) : base(output) { }

    [Scenario("Legacy {Token} is substituted with matching property value"), Fact]
    public async Task LegacyToken_Substituted()
    {
        var props = new Dictionary<string, object?> { ["Name"] = "Alice" };
        var result = PromptTemplateRenderer.Render("Hello {Name}!", props);

        await Given("template 'Hello {Name}!' with Name=Alice", () => result)
            .Then("output is 'Hello Alice!'", r =>
            {
                r.Should().Be("Hello Alice!");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Double-brace {{Token}} is substituted"), Fact]
    public async Task DoubleBraceToken_Substituted()
    {
        var props = new Dictionary<string, object?> { ["City"] = "Paris" };
        var result = PromptTemplateRenderer.Render("Visit {{ City }}", props);

        await Given("template 'Visit {{ City }}' with City=Paris", () => result)
            .Then("output contains 'Paris'", r =>
            {
                r.Should().Contain("Paris");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Unknown token is left unchanged"), Fact]
    public async Task UnknownToken_LeftUnchanged()
    {
        var props = new Dictionary<string, object?>();
        var result = PromptTemplateRenderer.Render("Hello {Unknown}!", props);

        await Given("template with unknown token and empty properties", () => result)
            .Then("placeholder is left intact", r =>
            {
                r.Should().Contain("{Unknown}");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null template returns empty string"), Fact]
    public async Task NullTemplate_ReturnsEmpty()
    {
        var result = PromptTemplateRenderer.Render(null, new Dictionary<string, object?>());

        await Given("a null template", () => result)
            .Then("result is empty string", r =>
            {
                r.Should().Be(string.Empty);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Empty template returns empty string"), Fact]
    public async Task EmptyTemplate_ReturnsEmpty()
    {
        var result = PromptTemplateRenderer.Render("", new Dictionary<string, object?>());

        await Given("an empty template", () => result)
            .Then("result is empty string", r =>
            {
                r.Should().Be(string.Empty);
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Null properties throws ArgumentNullException"), Fact]
    public async Task NullProperties_Throws()
    {
        Exception? caught = null;
        try { PromptTemplateRenderer.Render("test", null!); }
        catch (Exception ex) { caught = ex; }

        await Given("null properties dictionary", () => caught)
            .Then("ArgumentNullException is thrown", ex =>
            {
                ex.Should().BeOfType<ArgumentNullException>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Dot-path token resolves nested dictionary value"), Fact]
    public async Task DotPathToken_ResolvesNested()
    {
        var props = new Dictionary<string, object?>
        {
            ["Step"] = new Dictionary<string, object?> { ["Response"] = "ok" }
        };
        var result = PromptTemplateRenderer.Render("{{ Step.Response }}", props);

        await Given("dot-path token '{{ Step.Response }}' with nested dict", () => result)
            .Then("result contains 'ok'", r =>
            {
                r.Should().Contain("ok");
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Multiple tokens in same template are all substituted"), Fact]
    public async Task MultipleTokens_AllSubstituted()
    {
        var props = new Dictionary<string, object?> { ["A"] = "foo", ["B"] = "bar" };
        var result = PromptTemplateRenderer.Render("{A} and {B}", props);

        await Given("template '{A} and {B}' with A=foo, B=bar", () => result)
            .Then("output is 'foo and bar'", r =>
            {
                r.Should().Be("foo and bar");
                return true;
            })
            .AssertPassed();
    }
}
