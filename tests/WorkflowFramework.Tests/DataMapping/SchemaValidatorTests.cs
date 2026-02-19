using Xunit;
using FluentAssertions;
using WorkflowFramework.Extensions.DataMapping.Schema.Abstractions;
using WorkflowFramework.Extensions.DataMapping.Schema.Validators;

namespace WorkflowFramework.Tests.DataMapping;

public class JsonSchemaValidatorTests
{
    [Fact]
    public void Validate_ValidData_ReturnsValid()
    {
        var registry = new SchemaRegistry();
        registry.Register("person", """
        {
            "type": "object",
            "required": ["name", "age"],
            "properties": {
                "name": { "type": "string" },
                "age": { "type": "number" }
            }
        }
        """);

        var validator = new JsonSchemaValidator(registry);
        var result = validator.Validate("""{"name":"Alice","age":30}""", "person");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingRequired_ReturnsInvalid()
    {
        var registry = new SchemaRegistry();
        registry.Register("person", """
        {
            "type": "object",
            "required": ["name", "age"],
            "properties": {
                "name": { "type": "string" },
                "age": { "type": "number" }
            }
        }
        """);

        var validator = new JsonSchemaValidator(registry);
        var result = validator.Validate("""{"name":"Alice"}""", "person");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("age"));
    }

    [Fact]
    public void Validate_WrongType_ReturnsInvalid()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", """
        {
            "type": "object",
            "properties": {
                "count": { "type": "number" }
            }
        }
        """);

        var validator = new JsonSchemaValidator(registry);
        var result = validator.Validate("""{"count":"not a number"}""", "test");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_UnknownSchema_ReturnsInvalid()
    {
        var validator = new JsonSchemaValidator(new SchemaRegistry());
        var result = validator.Validate("{}", "nonexistent");
        result.IsValid.Should().BeFalse();
    }
}

public class XmlSchemaValidatorTests
{
    [Fact]
    public void Validate_ValidXml_ReturnsValid()
    {
        var registry = new SchemaRegistry();
        registry.Register("person", """
        <?xml version="1.0" encoding="utf-8"?>
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
          <xs:element name="person">
            <xs:complexType>
              <xs:sequence>
                <xs:element name="name" type="xs:string"/>
                <xs:element name="age" type="xs:int"/>
              </xs:sequence>
            </xs:complexType>
          </xs:element>
        </xs:schema>
        """);

        var validator = new XmlSchemaValidator(registry);
        var result = validator.Validate("<person><name>Alice</name><age>30</age></person>", "person");
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidXml_ReturnsInvalid()
    {
        var registry = new SchemaRegistry();
        registry.Register("person", """
        <?xml version="1.0" encoding="utf-8"?>
        <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
          <xs:element name="person">
            <xs:complexType>
              <xs:sequence>
                <xs:element name="name" type="xs:string"/>
                <xs:element name="age" type="xs:int"/>
              </xs:sequence>
            </xs:complexType>
          </xs:element>
        </xs:schema>
        """);

        var validator = new XmlSchemaValidator(registry);
        var result = validator.Validate("<person><name>Alice</name></person>", "person");
        result.IsValid.Should().BeFalse();
    }
}

public class SchemaRegistryTests
{
    [Fact]
    public void Register_And_Get()
    {
        var registry = new SchemaRegistry();
        registry.Register("test", "{}", 2);
        registry.GetSchema("test").Should().Be("{}");
        registry.GetSchemaVersion("test").Should().Be(2);
    }

    [Fact]
    public void GetSchemaNames_ReturnsAll()
    {
        var registry = new SchemaRegistry();
        registry.Register("a", "{}");
        registry.Register("b", "{}");
        registry.GetSchemaNames().Should().Contain("a").And.Contain("b");
    }
}
