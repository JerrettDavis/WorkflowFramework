using Xunit;
using System.Text.Json;
using System.Xml.Linq;
using FluentAssertions;
using WorkflowFramework.Extensions.DataMapping.Readers;

namespace WorkflowFramework.Tests.DataMapping;

public class JsonSourceReaderTests
{
    private readonly JsonSourceReader _reader = new();

    [Fact]
    public void CanRead_JsonPath_ReturnsTrue()
    {
        _reader.CanRead("$.name").Should().BeTrue();
    }

    [Fact]
    public void CanRead_NonJsonPath_ReturnsFalse()
    {
        _reader.CanRead("/root/name").Should().BeFalse();
    }

    [Fact]
    public void Read_SimpleProperty()
    {
        using var doc = JsonDocument.Parse("""{"name": "Alice"}""");
        _reader.Read("$.name", doc.RootElement).Should().Be("Alice");
    }

    [Fact]
    public void Read_NestedProperty()
    {
        using var doc = JsonDocument.Parse("""{"user":{"age":25}}""");
        _reader.Read("$.user.age", doc.RootElement).Should().Be("25");
    }

    [Fact]
    public void Read_ArrayIndex()
    {
        using var doc = JsonDocument.Parse("""{"items":[{"id":"A"},{"id":"B"}]}""");
        _reader.Read("$.items[1].id", doc.RootElement).Should().Be("B");
    }

    [Fact]
    public void Read_MissingPath_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("""{"name":"test"}""");
        _reader.Read("$.missing", doc.RootElement).Should().BeNull();
    }

    [Fact]
    public void Read_BooleanValue()
    {
        using var doc = JsonDocument.Parse("""{"active":true}""");
        _reader.Read("$.active", doc.RootElement).Should().Be("true");
    }
}

public class XmlSourceReaderTests
{
    private readonly XmlSourceReader _reader = new();

    [Fact]
    public void CanRead_XPath_ReturnsTrue()
    {
        _reader.CanRead("/root/name").Should().BeTrue();
    }

    [Fact]
    public void Read_SimpleElement()
    {
        var doc = XDocument.Parse("<root><name>Alice</name></root>");
        _reader.Read("/root/name", doc).Should().Be("Alice");
    }

    [Fact]
    public void Read_Nested()
    {
        var doc = XDocument.Parse("<root><user><age>25</age></user></root>");
        _reader.Read("/root/user/age", doc).Should().Be("25");
    }
}

public class DictionarySourceReaderTests
{
    private readonly DictionarySourceReader _reader = new();

    [Fact]
    public void CanRead_SimplePath()
    {
        _reader.CanRead("name").Should().BeTrue();
    }

    [Fact]
    public void CanRead_JsonPath_ReturnsFalse()
    {
        _reader.CanRead("$.name").Should().BeFalse();
    }

    [Fact]
    public void Read_SimpleKey()
    {
        var dict = new Dictionary<string, object?> { ["name"] = "Alice" };
        _reader.Read("name", dict).Should().Be("Alice");
    }

    [Fact]
    public void Read_NestedPath()
    {
        var inner = new Dictionary<string, object?> { ["name"] = "Bob" };
        var dict = new Dictionary<string, object?> { ["user"] = inner };
        _reader.Read("user.name", dict).Should().Be("Bob");
    }
}

public class ObjectSourceReaderTests
{
    private readonly ObjectSourceReader _reader = new();

    [Fact]
    public void CanRead_ObjectPath()
    {
        _reader.CanRead("@.Name").Should().BeTrue();
    }

    [Fact]
    public void Read_SimpleProperty()
    {
        var obj = new TestObj { Name = "Alice", Age = 30 };
        _reader.Read("@.Name", obj).Should().Be("Alice");
        _reader.Read("@.Age", obj).Should().Be("30");
    }

    [Fact]
    public void Read_NestedProperty()
    {
        var obj = new TestObj { Name = "Alice", Inner = new InnerObj { Value = "test" } };
        _reader.Read("@.Inner.Value", obj).Should().Be("test");
    }

    private class TestObj
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public InnerObj? Inner { get; set; }
    }

    private class InnerObj
    {
        public string Value { get; set; } = "";
    }
}
