using FluentAssertions;
using WorkflowFramework.Extensions.DataMapping.Schema.Abstractions;
using WorkflowFramework.Extensions.DataMapping.Schema.Validators;
using WorkflowFramework.Extensions.DataMapping.Steps;
using Xunit;

namespace WorkflowFramework.Tests.DataMapping;

public class SchemaValidatorExtendedTests
{
    [Fact]
    public void JsonSchemaValidator_NullProvider_Throws()
    {
        var act = () => new JsonSchemaValidator(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_MissingSchema_ReturnsInvalid()
    {
        var registry = new SchemaRegistry();
        var validator = new JsonSchemaValidator(registry);
        var result = validator.Validate("{}", "nonexistent");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not found"));
    }

    [Fact]
    public void Validate_ValidData_ReturnsValid()
    {
        var registry = new SchemaRegistry();
        registry.Register("person", """{"type":"object","required":["name"],"properties":{"name":{"type":"string"}}}""");
        var validator = new JsonSchemaValidator(registry);
        var result = validator.Validate("""{"name":"John"}""", "person");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingRequiredField_ReturnsInvalid()
    {
        var registry = new SchemaRegistry();
        registry.Register("person", """{"type":"object","required":["name"],"properties":{"name":{"type":"string"}}}""");
        var validator = new JsonSchemaValidator(registry);
        var result = validator.Validate("""{"age":30}""", "person");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("name"));
    }

    [Fact]
    public void Validate_WrongType_ReturnsInvalid()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", """{"type":"object","properties":{"age":{"type":"number"}}}""");
        var validator = new JsonSchemaValidator(registry);
        var result = validator.Validate("""{"age":"not-a-number"}""", "test");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_InvalidJson_ReturnsInvalid()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", """{"type":"object"}""");
        var validator = new JsonSchemaValidator(registry);
        var result = validator.Validate("not json", "test");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void SchemaRegistry_RegisterAndGet()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", "{}", 2);
        registry.GetSchema("test").Should().Be("{}");
        registry.GetSchemaVersion("test").Should().Be(2);
    }

    [Fact]
    public void SchemaRegistry_GetMissing_ReturnsNull()
    {
        var registry = new SchemaRegistry();
        registry.GetSchema("missing").Should().BeNull();
        registry.GetSchemaVersion("missing").Should().BeNull();
    }

    [Fact]
    public void SchemaRegistry_GetSchemaNames()
    {
        var registry = new SchemaRegistry();
        registry.Register("a", "{}");
        registry.Register("b", "{}");
        registry.GetSchemaNames().Should().Contain("a").And.Contain("b");
    }

    [Fact]
    public void SchemaValidationResult_Valid()
    {
        var result = SchemaValidationResult.Valid();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void SchemaValidationResult_Invalid()
    {
        var result = SchemaValidationResult.Invalid(new[] { "error" });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("error");
    }
}
