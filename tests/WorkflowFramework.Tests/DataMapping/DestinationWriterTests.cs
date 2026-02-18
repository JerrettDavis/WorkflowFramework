using Xunit;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using FluentAssertions;
using WorkflowFramework.Extensions.DataMapping.Writers;

namespace WorkflowFramework.Tests.DataMapping;

public class JsonDestinationWriterTests
{
    private readonly JsonDestinationWriter _writer = new();

    [Fact]
    public void Write_SimpleProperty()
    {
        var obj = new JsonObject();
        _writer.Write("$.name", "Alice", obj).Should().BeTrue();
        obj["name"]!.GetValue<string>().Should().Be("Alice");
    }

    [Fact]
    public void Write_NestedProperty_CreatesIntermediateObjects()
    {
        var obj = new JsonObject();
        _writer.Write("$.user.name", "Bob", obj).Should().BeTrue();
        obj["user"]!["name"]!.GetValue<string>().Should().Be("Bob");
    }

    [Fact]
    public void Write_NullValue()
    {
        var obj = new JsonObject();
        _writer.Write("$.status", null, obj).Should().BeTrue();
        // null is written as a JsonValue(null), node exists but value is null
        obj.ContainsKey("status").Should().BeTrue();
    }
}

public class XmlDestinationWriterTests
{
    private readonly XmlDestinationWriter _writer = new();

    [Fact]
    public void Write_SimpleElement()
    {
        var doc = new XDocument(new XElement("root"));
        _writer.Write("/root/name", "Alice", doc).Should().BeTrue();
        doc.Root!.Element("name")!.Value.Should().Be("Alice");
    }

    [Fact]
    public void Write_NestedElement()
    {
        var doc = new XDocument(new XElement("root"));
        _writer.Write("/root/user/name", "Bob", doc).Should().BeTrue();
        doc.Root!.Element("user")!.Element("name")!.Value.Should().Be("Bob");
    }
}

public class DictionaryDestinationWriterTests
{
    private readonly DictionaryDestinationWriter _writer = new();

    [Fact]
    public void Write_SimpleKey()
    {
        var dict = new Dictionary<string, object?>();
        _writer.Write("name", "Alice", dict).Should().BeTrue();
        dict["name"].Should().Be("Alice");
    }

    [Fact]
    public void Write_NestedKey()
    {
        var dict = new Dictionary<string, object?>();
        _writer.Write("user.name", "Bob", dict).Should().BeTrue();
        ((IDictionary<string, object?>)dict["user"]!)["name"].Should().Be("Bob");
    }
}

public class ObjectDestinationWriterTests
{
    private readonly ObjectDestinationWriter _writer = new();

    [Fact]
    public void Write_SimpleProperty()
    {
        var obj = new TestDest();
        _writer.Write("@.Name", "Alice", obj).Should().BeTrue();
        obj.Name.Should().Be("Alice");
    }

    [Fact]
    public void Write_IntProperty()
    {
        var obj = new TestDest();
        _writer.Write("@.Age", "25", obj).Should().BeTrue();
        obj.Age.Should().Be(25);
    }

    private class TestDest
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }
}
