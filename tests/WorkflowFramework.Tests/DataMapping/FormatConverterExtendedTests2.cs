using FluentAssertions;
using WorkflowFramework.Extensions.DataMapping.Abstractions;
using WorkflowFramework.Extensions.DataMapping.Formats.Converters;
using Xunit;

namespace WorkflowFramework.Tests.DataMapping;

public class FormatConverterExtendedTests2
{
    private readonly FormatConverter _converter = new();

    [Fact]
    public void JsonToXml_NestedObject()
    {
        var json = """{"person":{"name":"Alice","age":"30"}}""";
        var xml = _converter.Convert(json, DataFormat.Json, DataFormat.Xml);
        xml.Should().Contain("<person>");
        xml.Should().Contain("<name>Alice</name>");
    }

    [Fact]
    public void JsonToXml_Array()
    {
        var json = """[1,2,3]""";
        var xml = _converter.Convert(json, DataFormat.Json, DataFormat.Xml);
        xml.Should().Contain("<item>");
    }

    [Fact]
    public void JsonToXml_SanitizesInvalidXmlNames()
    {
        // Property name starting with a digit
        var json = """{"1abc":"val"}""";
        var xml = _converter.Convert(json, DataFormat.Json, DataFormat.Xml);
        xml.Should().Contain("<_abc>"); // digit replaced with _
    }

    [Fact]
    public void JsonToXml_SanitizesSpecialChars()
    {
        var json = """{"a b":"val"}""";
        var xml = _converter.Convert(json, DataFormat.Json, DataFormat.Xml);
        xml.Should().Contain("<a_b>"); // space replaced with _
    }

    [Fact]
    public void JsonToXml_EmptyName_ReturnsUnderscore()
    {
        // Test SanitizeXmlName with empty string via reflection isn't easy,
        // but we can test through JSON with empty property name
        var json = """{"":"val"}""";
        var xml = _converter.Convert(json, DataFormat.Json, DataFormat.Xml);
        xml.Should().Contain("<_>");
    }

    [Fact]
    public void XmlToJson_NestedElements()
    {
        var xml = "<root><person><name>Alice</name></person></root>";
        var json = _converter.Convert(xml, DataFormat.Xml, DataFormat.Json);
        json.Should().Contain("\"person\"");
        json.Should().Contain("\"name\"");
    }

    [Fact]
    public void CsvToJson_EmptyInput_ReturnsEmptyArray()
    {
        var result = _converter.Convert("", DataFormat.Csv, DataFormat.Json);
        result.Should().Contain("[]");
    }

    [Fact]
    public void JsonToCsv_WithQuotedFields()
    {
        var json = """[{"name":"Alice, Jr.","age":"30"}]""";
        var csv = _converter.Convert(json, DataFormat.Json, DataFormat.Csv);
        csv.Should().Contain("\"Alice, Jr.\"");
    }

    [Fact]
    public void JsonToCsv_WithMissingProperty()
    {
        var json = """[{"name":"Alice","age":"30"},{"name":"Bob"}]""";
        var csv = _converter.Convert(json, DataFormat.Json, DataFormat.Csv);
        csv.Should().Contain("name,age");
        // Bob's age should be empty
        csv.Should().Contain("Bob,");
    }

    [Fact]
    public void CsvToJson_WithQuotedFields()
    {
        var csv = "name,desc\nAlice,\"Hello, World\"\n";
        var json = _converter.Convert(csv, DataFormat.Csv, DataFormat.Json);
        json.Should().Contain("Hello, World");
    }

    [Fact]
    public void DetectFormat_WhitespaceOnly_ReturnsJson()
    {
        _converter.DetectFormat("   ").Should().Be(DataFormat.Json);
    }

    [Fact]
    public void DetectFormat_PlainText_ReturnsJson()
    {
        _converter.DetectFormat("hello world").Should().Be(DataFormat.Json);
    }

    [Fact]
    public void Convert_XmlToCsv_Unsupported_Throws()
    {
        var act = () => _converter.Convert("<root/>", DataFormat.Xml, DataFormat.Csv);
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Convert_CsvToXml_Unsupported_Throws()
    {
        var act = () => _converter.Convert("a,b\n1,2", DataFormat.Csv, DataFormat.Xml);
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void JsonToCsv_WithQuoteInValue()
    {
        var json = """[{"name":"She said \"hi\"","age":"30"}]""";
        var csv = _converter.Convert(json, DataFormat.Json, DataFormat.Csv);
        csv.Should().Contain("\"\""); // escaped quotes
    }

    [Fact]
    public void JsonToXml_ValidCharsPreserved()
    {
        var json = """{"my-prop":"val","my.prop2":"val2","_start":"val3"}""";
        var xml = _converter.Convert(json, DataFormat.Json, DataFormat.Xml);
        xml.Should().Contain("<my-prop>");
        xml.Should().Contain("<my.prop2>");
        xml.Should().Contain("<_start>");
    }
}
