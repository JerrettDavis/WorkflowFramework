using System.Xml.Linq;
using System.Xml.XPath;
using WorkflowFramework.Extensions.DataMapping.Abstractions;

namespace WorkflowFramework.Extensions.DataMapping.Readers;

/// <summary>
/// Reads values from an <see cref="XDocument"/> using XPath expressions.
/// Paths must start with <c>/</c> or <c>//</c>.
/// </summary>
public sealed class XmlSourceReader : ISourceReader<XDocument>
{
    /// <inheritdoc />
    public IReadOnlyList<string> SupportedPrefixes => ["/"];

    /// <inheritdoc />
    public bool CanRead(string path) => path.StartsWith("/", StringComparison.Ordinal);

    /// <inheritdoc />
    public string? Read(string path, XDocument source)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        try
        {
            var result = source.XPathEvaluate(path);
            if (result is IEnumerable<object> enumerable)
            {
                var first = enumerable.FirstOrDefault();
                return first switch
                {
                    XElement el => el.Value,
                    XAttribute attr => attr.Value,
                    XText text => text.Value,
                    _ => first?.ToString()
                };
            }
            return result?.ToString();
        }
        catch
        {
            return null;
        }
    }
}
