using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using WorkflowFramework.Extensions.DataMapping.Abstractions;
using WorkflowFramework.Extensions.DataMapping.Steps;

namespace WorkflowFramework.Extensions.DataMapping.Formats.Converters;

/// <summary>
/// Converts data between JSON, XML, CSV, and YAML string formats.
/// </summary>
public sealed class FormatConverter : IFormatConverter
{
    /// <inheritdoc />
    public string Convert(string input, DataFormat from, DataFormat to)
    {
        if (from == to) return input;

        return (from, to) switch
        {
            (DataFormat.Json, DataFormat.Xml) => JsonToXml(input),
            (DataFormat.Xml, DataFormat.Json) => XmlToJson(input),
            (DataFormat.Json, DataFormat.Csv) => JsonToCsv(input),
            (DataFormat.Csv, DataFormat.Json) => CsvToJson(input),
            _ => throw new NotSupportedException($"Conversion from {from} to {to} is not supported.")
        };
    }

    /// <inheritdoc />
    public DataFormat DetectFormat(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return DataFormat.Json;

        var trimmed = content.TrimStart();
        if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            return DataFormat.Json;
        if (trimmed.StartsWith("<"))
            return DataFormat.Xml;
        if (trimmed.Contains(",") && trimmed.Contains("\n"))
            return DataFormat.Csv;

        return DataFormat.Json;
    }

    private static string JsonToXml(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = new XElement("root");
        JsonElementToXml(doc.RootElement, root);
        return new XDocument(root).ToString();
    }

    private static void JsonElementToXml(JsonElement element, XElement parent)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var child = new XElement(SanitizeXmlName(prop.Name));
                    JsonElementToXml(prop.Value, child);
                    parent.Add(child);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var child = new XElement("item");
                    JsonElementToXml(item, child);
                    parent.Add(child);
                }
                break;
            default:
                parent.Value = element.ToString();
                break;
        }
    }

    private static string SanitizeXmlName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "_";
        // Replace invalid XML name characters
        var sb = new StringBuilder(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i == 0 && !char.IsLetter(c) && c != '_')
                sb.Append('_');
            else if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.')
                sb.Append('_');
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    private static string XmlToJson(string xml)
    {
        var doc = XDocument.Parse(xml);
        var obj = new JsonObject();
        if (doc.Root != null)
            XmlElementToJson(doc.Root, obj);
        return obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static void XmlElementToJson(XElement element, JsonObject parent)
    {
        if (!element.HasElements)
        {
            parent[element.Name.LocalName] = element.Value;
            return;
        }

        var child = new JsonObject();
        foreach (var sub in element.Elements())
            XmlElementToJson(sub, child);
        parent[element.Name.LocalName] = child;
    }

    private static string JsonToCsv(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("JSON to CSV conversion requires an array.");

        var sb = new StringBuilder();
        var headers = new List<string>();
        var first = true;

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (first)
            {
                foreach (var prop in item.EnumerateObject())
                    headers.Add(prop.Name);
                sb.AppendLine(string.Join(",", headers));
                first = false;
            }

            var values = headers.Select(h =>
            {
                if (item.TryGetProperty(h, out var val))
                {
                    var v = val.ToString();
                    return v.Contains(',') || v.Contains('"') || v.Contains('\n')
                        ? $"\"{v.Replace("\"", "\"\"")}\""
                        : v;
                }
                return string.Empty;
            });
            sb.AppendLine(string.Join(",", values));
        }

        return sb.ToString();
    }

    private static string CsvToJson(string csv)
    {
        var lines = csv.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            return "[]";

        var headers = ParseCsvLine(lines[0]);
        var array = new JsonArray();

        for (var i = 1; i < lines.Length; i++)
        {
            var values = ParseCsvLine(lines[i]);
            var obj = new JsonObject();
            for (var j = 0; j < headers.Length && j < values.Length; j++)
                obj[headers[j]] = values[j];
            array.Add(obj);
        }

        return array.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var inQuote = false;
        var field = new StringBuilder();

        foreach (var c in line.TrimEnd('\r'))
        {
            if (c == '"')
            {
                inQuote = !inQuote;
            }
            else if (c == ',' && !inQuote)
            {
                result.Add(field.ToString().Trim());
                field.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        result.Add(field.ToString().Trim());
        return result.ToArray();
    }
}
