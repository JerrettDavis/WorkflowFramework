using FluentAssertions;
using WorkflowFramework.Extensions.Expressions;
using Xunit;

namespace WorkflowFramework.Tests.Extensions.Expressions;

public class TemplateEngineTests
{
    private readonly TemplateEngine _engine = new();
    private readonly Dictionary<string, object?> _vars = new();

    [Fact]
    public async Task RenderAsync_NullTemplate_ReturnsNull()
    {
        var result = await _engine.RenderAsync(null!, _vars);
        result.Should().BeNull();
    }

    [Fact]
    public async Task RenderAsync_EmptyTemplate_ReturnsEmpty()
    {
        var result = await _engine.RenderAsync("", _vars);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RenderAsync_NoPlaceholders_ReturnsOriginal()
    {
        var result = await _engine.RenderAsync("plain text", _vars);
        result.Should().Be("plain text");
    }

    [Fact]
    public async Task RenderAsync_SingleVariable()
    {
        _vars["name"] = "World";
        var result = await _engine.RenderAsync("Hello {{name}}!", _vars);
        result.Should().Be("Hello World!");
    }

    [Fact]
    public async Task RenderAsync_MultipleVariables()
    {
        _vars["first"] = "John";
        _vars["last"] = "Doe";
        var result = await _engine.RenderAsync("{{first}} {{last}}", _vars);
        result.Should().Be("John Doe");
    }

    [Fact]
    public async Task RenderAsync_Expression()
    {
        _vars["x"] = 10.0;
        var result = await _engine.RenderAsync("Result: {{x + 5}}", _vars);
        result.Should().Be("Result: 15");
    }

    [Fact]
    public async Task RenderAsync_NullVariable_RendersEmpty()
    {
        var result = await _engine.RenderAsync("Value: {{null}}", _vars);
        result.Should().Be("Value: ");
    }

    [Fact]
    public async Task RenderAsync_BooleanExpression()
    {
        var result = await _engine.RenderAsync("Is: {{true}}", _vars);
        result.Should().Be("Is: True");
    }

    [Fact]
    public void Constructor_WithCustomEvaluator()
    {
        var custom = new SimpleExpressionEvaluator();
        var engine = new TemplateEngine(custom);
        engine.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_NullEvaluator_UsesDefault()
    {
        var engine = new TemplateEngine(null);
        engine.Should().NotBeNull();
    }
}
