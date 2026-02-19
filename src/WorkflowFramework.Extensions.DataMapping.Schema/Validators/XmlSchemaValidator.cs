using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using WorkflowFramework.Extensions.DataMapping.Schema.Abstractions;
using WorkflowFramework.Extensions.DataMapping.Steps;

namespace WorkflowFramework.Extensions.DataMapping.Schema.Validators;

/// <summary>
/// Validates XML data against XSD schemas stored in a <see cref="SchemaRegistry"/>.
/// </summary>
public sealed class XmlSchemaValidator : ISchemaValidator
{
    private readonly ISchemaProvider _schemaProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="XmlSchemaValidator"/>.
    /// </summary>
    /// <param name="schemaProvider">The schema provider.</param>
    public XmlSchemaValidator(ISchemaProvider schemaProvider)
    {
        _schemaProvider = schemaProvider ?? throw new ArgumentNullException(nameof(schemaProvider));
    }

    /// <inheritdoc />
    public SchemaValidationResult Validate(string data, string schemaName)
    {
        var schemaStr = _schemaProvider.GetSchema(schemaName);
        if (schemaStr == null)
            return SchemaValidationResult.Invalid([$"Schema '{schemaName}' not found."]);

        try
        {
            var schemaSet = new XmlSchemaSet();
            using var schemaReader = new StringReader(schemaStr);
            schemaSet.Add(string.Empty, XmlReader.Create(schemaReader));

            var errors = new List<string>();
            var doc = XDocument.Parse(data);
            doc.Validate(schemaSet, (_, e) => errors.Add(e.Message));

            return errors.Count == 0
                ? SchemaValidationResult.Valid()
                : SchemaValidationResult.Invalid(errors);
        }
        catch (Exception ex) when (ex is XmlException or XmlSchemaException)
        {
            return SchemaValidationResult.Invalid([$"Validation error: {ex.Message}"]);
        }
    }
}
