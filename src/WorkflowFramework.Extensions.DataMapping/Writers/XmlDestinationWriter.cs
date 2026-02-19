using System.Xml.Linq;
using WorkflowFramework.Extensions.DataMapping.Abstractions;

namespace WorkflowFramework.Extensions.DataMapping.Writers;

/// <summary>
/// Writes values to an <see cref="XDocument"/> using simple XPath-like paths.
/// Creates intermediate elements as needed. Paths start with <c>/</c>.
/// The document must already have a root element.
/// </summary>
public sealed class XmlDestinationWriter : IDestinationWriter<XDocument>
{
    /// <inheritdoc />
    public IReadOnlyList<string> SupportedPrefixes => ["/"];

    /// <inheritdoc />
    public bool CanWrite(string path) => path.StartsWith("/", StringComparison.Ordinal);

    /// <inheritdoc />
    public bool Write(string path, string? value, XDocument destination)
    {
        if (string.IsNullOrEmpty(path) || destination.Root == null)
            return false;

        try
        {
            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                return false;

            var current = destination.Root;

            // If root name doesn't match first segment, fail
            if (current.Name.LocalName != segments[0])
                return false;

            for (var i = 1; i < segments.Length - 1; i++)
            {
                var child = current.Element(segments[i]);
                if (child == null)
                {
                    child = new XElement(segments[i]);
                    current.Add(child);
                }
                current = child;
            }

            if (segments.Length > 1)
            {
                var leaf = current.Element(segments[segments.Length - 1]);
                if (leaf == null)
                {
                    leaf = new XElement(segments[segments.Length - 1]);
                    current.Add(leaf);
                }
                leaf.Value = value ?? string.Empty;
            }
            else
            {
                current.Value = value ?? string.Empty;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
