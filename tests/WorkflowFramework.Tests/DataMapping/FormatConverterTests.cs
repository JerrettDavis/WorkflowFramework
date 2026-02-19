using Xunit;
using FluentAssertions;
using WorkflowFramework.Extensions.DataMapping.Abstractions;
using WorkflowFramework.Extensions.DataMapping.Formats.Converters;

namespace WorkflowFramework.Tests.DataMapping;

public class FormatConverterTests
{
    private readonly FormatConverter _converter = new();

    [Fact]
    public void JsonToXml_ConvertsSimpleObject()
    {
        var json = """{"name":"Alice","age":"30"}""";
        var xml = _converter.Convert(json, DataFormat.Json, DataFormat.Xml);
        xml.Should().Contain("<name>Alice</name>");
        xml.Should().Contain("<age>30</age>");
    }

    [Fact]
    public void XmlToJson_ConvertsSimpleElement()
    {
        var xml = "<root><name>Alice</name><age>30</age></root>";
        var json = _converter.Convert(xml, DataFormat.Xml, DataFormat.Json);
        json.Should().Contain("\"name\"");
        json.Should().Contain("Alice");
    }

    [Fact]
    public void JsonToCsv_ConvertsArray()
    {
        var json = """[{"name":"Alice","age":"30"},{"name":"Bob","age":"25"}]""";
        var csv = _converter.Convert(json, DataFormat.Json, DataFormat.Csv);
        csv.Should().Contain("name,age");
        csv.Should().Contain("Alice,30");
        csv.Should().Contain("Bob,25");
    }

    [Fact]
    public void CsvToJson_ConvertsToArray()
    {
        var csv = "name,age\nAlice,30\nBob,25\n";
        var json = _converter.Convert(csv, DataFormat.Csv, DataFormat.Json);
        json.Should().Contain("Alice");
        json.Should().Contain("Bob");
    }

    [Fact]
    public void SameFormat_ReturnsInput()
    {
        var json = """{"a":"b"}""";
        _converter.Convert(json, DataFormat.Json, DataFormat.Json).Should().Be(json);
    }

    [Fact]
    public void DetectFormat_Json()
    {
        _converter.DetectFormat("""{"a":1}""").Should().Be(DataFormat.Json);
        _converter.DetectFormat("[1,2,3]").Should().Be(DataFormat.Json);
    }

    [Fact]
    public void DetectFormat_Xml()
    {
        _converter.DetectFormat("<root/>").Should().Be(DataFormat.Xml);
    }

    [Fact]
    public void DetectFormat_Csv()
    {
        _converter.DetectFormat("a,b\n1,2\n").Should().Be(DataFormat.Csv);
    }
}
