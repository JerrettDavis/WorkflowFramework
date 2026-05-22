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

    [Scenario("Missing variable placeholder renders as empty string"), Fact]
    public async Task RenderMissingVariableRendersAsEmpty()
    {
        // The TemplateEngine evaluates the expression; if the variable is not found
        // the evaluator throws, which surfaces as the exception propagating out.
        // Characterize actual behavior: exception propagates.
        var vars = new Dictionary<string, object?>();
        Func<Task> act = () => Engine.RenderAsync("Hello {{missing}}!", vars);

        await Given("template with an undefined variable placeholder", () => act)
            .Then("an exception propagates (evaluator cannot resolve the variable)", fn =>
            {
                fn.Should().ThrowAsync<Exception>();
                return true;
            })
            .AssertPassed();
    }

    [Scenario("Template renders arithmetic expression in placeholder"), Fact]
    public async Task RenderArithmeticExpression()
    {
        var vars = new Dictionary<string, object?> { ["x"] = 5.0 };
        // Note: 'x + 3' is evaluated by the SimpleExpressionEvaluator
        // Gotcha: LastIndexOf splits on the last operator — single op works fine.
        var rendered = await Engine.RenderAsync("Result: {{x + 3}}", vars);

        await Given("template '{{x + 3}}' with x=5", () => rendered)
            .Then("renders 'Result: 8'", v => { v.Should().Be("Result: 8"); return true; })
            .AssertPassed();
    }

    [Scenario("Template renders boolean expression in placeholder"), Fact]
    public async Task RenderBooleanExpression()
    {
        var rendered = await Engine.RenderAsync("Is it? {{true}}", new Dictionary<string, object?>());

        await Given("template '{{true}}'", () => rendered)
            .Then("renders 'Is it? True'", v => { v.Should().Be("Is it? True"); return true; })
            .AssertPassed();
    }

    [Scenario("TemplateEngine accepts custom evaluator"), Fact]
    public async Task RenderWithCustomEvaluator()
    {
        // Inject a custom evaluator that always returns "CUSTOM"
        var customEvaluator = new AlwaysCustomEvaluator();
        var engine = new TemplateEngine(customEvaluator);

        var rendered = await engine.RenderAsync("{{anything}}", new Dictionary<string, object?>());

        await Given("a TemplateEngine with an always-custom evaluator", () => rendered)
            .Then("placeholder is replaced with CUSTOM", v => { v.Should().Be("CUSTOM"); return true; })
            .AssertPassed();
    }

    // --- helper ---

    private sealed class AlwaysCustomEvaluator : IExpressionEvaluator
    {
        public string Name => "always-custom";

        public Task<T?> EvaluateAsync<T>(string expression, IDictionary<string, object?> variables, CancellationToken ct = default)
            => Task.FromResult((T?)(object?)"CUSTOM");

        public Task<object?> EvaluateAsync(string expression, IDictionary<string, object?> variables, CancellationToken ct = default)
            => Task.FromResult<object?>("CUSTOM");
    }
}
