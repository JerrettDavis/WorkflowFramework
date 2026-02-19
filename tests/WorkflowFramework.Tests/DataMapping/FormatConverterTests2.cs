using FluentAssertions;
using WorkflowFramework.Extensions.DataMapping.Abstractions;
using WorkflowFramework.Extensions.DataMapping.Formats.Converters;
using Xunit;

namespace WorkflowFramework.Tests.DataMapping;

public class FormatConverterExtendedTests
{
    private readonly FormatConverter _converter = new();

    [Fact]
    public void SameFormat_ReturnsInput()
    {
        var input = "{\"a\":1}";
        _converter.Convert(input, DataFormat.Json, DataFormat.Json).Should().Be(input);
    }

    [Fact]
    public void JsonToXml_SimpleObject()
    {
        var json = """{"name":"test","value":"42"}""";
        var xml = _converter.Convert(json, DataFormat.Json, DataFormat.Xml);
        xml.Should().Contain("<name>test</name>");
        xml.Should().Contain("<value>42</value>");
    }

    [Fact]
    public void XmlToJson_SimpleObject()
    {
        var xml = "<root><name>test</name></root>";
        var json = _converter.Convert(xml, DataFormat.Xml, DataFormat.Json);
        json.Should().Contain("\"name\"");
        json.Should().Contain("test");
    }

    [Fact]
    public void JsonToCsv_Array()
    {
        var json = """[{"name":"a","age":"1"},{"name":"b","age":"2"}]""";
        var csv = _converter.Convert(json, DataFormat.Json, DataFormat.Csv);
        csv.Should().Contain("name,age");
        csv.Should().Contain("a,1");
        csv.Should().Contain("b,2");
    }

    [Fact]
    public void JsonToCsv_NotArray_Throws()
    {
        var act = () => _converter.Convert("{}", DataFormat.Json, DataFormat.Csv);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CsvToJson_RoundTrip()
    {
        var csv = "name,age\na,1\nb,2\n";
        var json = _converter.Convert(csv, DataFormat.Csv, DataFormat.Json);
        json.Should().Contain("\"name\"");
        json.Should().Contain("\"a\"");
    }

    [Fact]
    public void UnsupportedConversion_Throws()
    {
        var act = () => _converter.Convert("data", DataFormat.Yaml, DataFormat.Csv);
        act.Should().Throw<NotSupportedException>();
    }

    [Theory]
    [InlineData("{\"a\":1}", DataFormat.Json)]
    [InlineData("[1,2]", DataFormat.Json)]
    [InlineData("<root/>", DataFormat.Xml)]
    public void DetectFormat_DetectsCorrectly(string content, DataFormat expected)
    {
        _converter.DetectFormat(content).Should().Be(expected);
    }

    [Fact]
    public void DetectFormat_EmptyContent_DefaultsToJson()
    {
        _converter.DetectFormat("").Should().Be(DataFormat.Json);
    }

    [Fact]
    public void DetectFormat_Csv_WithCommaAndNewline()
    {
        _converter.DetectFormat("a,b\n1,2").Should().Be(DataFormat.Csv);
    }
}
