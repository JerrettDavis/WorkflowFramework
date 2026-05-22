using FluentAssertions;
using TinyBDD;
using TinyBDD.Xunit;
using WorkflowFramework.Extensions.Expressions.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace WorkflowFramework.Extensions.Expressions.Tests;

[Feature("TemplateEngine")]
public class TemplateEngineScenarios : ExpressionsTestBase
{
    public TemplateEngineScenarios(ITestOutputHelper output) : base(output) { }

    private static readonly TemplateEngine Engine = new();

    [Scenario("Renders a template with a single variable substitution"), Fact]
    public async Task RenderSingleSubstitution()
    {
        var vars = new Dictionary<string, object?> { ["name"] = "World" };
        var rendered = await Engine.RenderAsync("Hello, {{name}}!", vars);

        await Given("template 'Hello, {{name}}!' with name=World", () => rendered)
            .Then("renders 'Hello, World!'", v => { v.Should().Be("Hello, World!"); return true; })
            .AssertPassed();
    }

    [Scenario("Renders a template with multiple substitutions"), Fact]
    public async Task RenderMultipleSubstitutions()
    {
        var vars = new Dictionary<string, object?> { ["a"] = 1.0, ["b"] = 2.0 };
        // Use pre-combined: the evaluator can evaluate the variable
        var rendered = await Engine.RenderAsync("{{a}} and {{b}}", vars);

        await Given("template '{{a}} and {{b}}' with a=1, b=2", () => rendered)
            .Then("renders '1 and 2'", v => { v.Should().Be("1 and 2"); return true; })
            .AssertPassed();
    }

    [Scenario("Returns empty string for empty template"), Fact]
    public async Task RenderEmptyTemplate()
    {
        var rendered = await Engine.RenderAsync(string.Empty, new Dictionary<string, object?>());

        await Given("an empty template", () => rendered)
            .Then("renders empty string", v => { v.Should().BeEmpty(); return true; })
            .AssertPassed();
    }

    [Scenario("Leaves template text unchanged when no placeholders present"), Fact]
    public async Task RenderNoPLaceholders()
    {
        const string template = "no placeholders here";
        var rendered = await Engine.RenderAsync(template, new Dictionary<string, object?>());

        await Given("template with no placeholders", () => rendered)
            .Then("returns template unchanged", v => { v.Should().Be(template); return true; })
            .AssertPassed();
    }
}
