using FluentAssertions;
using WorkflowFramework.Extensions.DataMapping.Schema.Abstractions;
using WorkflowFramework.Extensions.DataMapping.Schema.Validators;
using Xunit;

namespace WorkflowFramework.Tests.DataMapping;

public class JsonSchemaValidatorExtendedTests2
{
    [Fact]
    public void Validate_ArrayType_Valid()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", """{"type":"array"}""");
        var validator = new JsonSchemaValidator(registry);
        var result = validator.Validate("[1,2,3]", "test");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ArrayType_Invalid()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", """{"type":"array"}""");
        var validator = new JsonSchemaValidator(registry);
        var result = validator.Validate("""{"a":1}""", "test");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_BooleanType_Valid()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", """{"type":"boolean"}""");
        var validator = new JsonSchemaValidator(registry);
        var result = validator.Validate("true", "test");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_BooleanType_Invalid()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", """{"type":"boolean"}""");
        var validator = new JsonSchemaValidator(registry);
        var result = validator.Validate("42", "test");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NullType_Valid()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", """{"type":"null"}""");
        var validator = new JsonSchemaValidator(registry);
        var result = validator.Validate("null", "test");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NullType_Invalid()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", """{"type":"null"}""");
        var validator = new JsonSchemaValidator(registry);
        var result = validator.Validate("42", "test");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_IntegerType_Valid()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", """{"type":"integer"}""");
        var validator = new JsonSchemaValidator(registry);
        var result = validator.Validate("42", "test");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_StringType_Valid()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", """{"type":"string"}""");
        var validator = new JsonSchemaValidator(registry);
        var result = validator.Validate("\"hello\"", "test");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_StringType_Invalid()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", """{"type":"string"}""");
        var validator = new JsonSchemaValidator(registry);
        var result = validator.Validate("42", "test");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NestedProperties_ValidatesRecursively()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", """
        {
            "type":"object",
            "properties": {
                "address": {
                    "type":"object",
                    "required": ["city"],
                    "properties": {
                        "city": {"type":"string"}
                    }
                }
            }
        }
        """);
        var validator = new JsonSchemaValidator(registry);

        var validResult = validator.Validate("""{"address":{"city":"NYC"}}""", "test");
        validResult.IsValid.Should().BeTrue();

        var invalidResult = validator.Validate("""{"address":{}}""", "test");
        invalidResult.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_NoTypeInSchema_PassesValidation()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", """{"required":["name"]}""");
        var validator = new JsonSchemaValidator(registry);
        var result = validator.Validate("""{"name":"test"}""", "test");
        result.IsValid.Should().BeTrue();
    }
}

public class XmlSchemaValidatorExtendedTests2
{
    [Fact]
    public void Constructor_NullProvider_Throws()
    {
        var act = () => new XmlSchemaValidator(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_MissingSchema_ReturnsInvalid()
    {
        var registry = new SchemaRegistry();
        var validator = new XmlSchemaValidator(registry);
        var result = validator.Validate("<root/>", "nonexistent");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not found"));
    }

    [Fact]
    public void Validate_MalformedXml_ReturnsInvalid()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", """
        <?xml version="1.0" encoding="utf-8"?>
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
          <xs:element name="root" type="xs:string"/>
        </xs:schema>
        """);
        var validator = new XmlSchemaValidator(registry);
        var result = validator.Validate("<<<not xml", "test");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Validation error"));
    }

    [Fact]
    public void Validate_InvalidSchema_ReturnsInvalid()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", "not a valid xsd");
        var validator = new XmlSchemaValidator(registry);
        var result = validator.Validate("<root/>", "test");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ValidSimpleElement()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", """
        <?xml version="1.0" encoding="utf-8"?>
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
          <xs:element name="name" type="xs:string"/>
        </xs:schema>
        """);
        var validator = new XmlSchemaValidator(registry);
        var result = validator.Validate("<name>hello</name>", "test");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WrongElement_ReturnsInvalid()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", """
        <?xml version="1.0" encoding="utf-8"?>
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
          <xs:element name="name" type="xs:string"/>
        </xs:schema>
        """);
        var validator = new XmlSchemaValidator(registry);
        var result = validator.Validate("<wrong>hello</wrong>", "test");
        result.IsValid.Should().BeFalse();
    }
}
