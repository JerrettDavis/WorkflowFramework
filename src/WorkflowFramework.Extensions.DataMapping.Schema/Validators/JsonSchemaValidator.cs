using System.Text.Json;
using WorkflowFramework.Extensions.DataMapping.Schema.Abstractions;
using WorkflowFramework.Extensions.DataMapping.Steps;

namespace WorkflowFramework.Extensions.DataMapping.Schema.Validators;

/// <summary>
/// Validates JSON data against schemas stored in a <see cref="SchemaRegistry"/>.
/// Performs basic structural validation (required fields, type checks).
/// For full JSON Schema Draft support, use a dedicated library.
/// </summary>
public sealed class JsonSchemaValidator : ISchemaValidator
{
    private readonly ISchemaProvider _schemaProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="JsonSchemaValidator"/>.
    /// </summary>
    /// <param name="schemaProvider">The schema provider.</param>
    public JsonSchemaValidator(ISchemaProvider schemaProvider)
    {
        _schemaProvider = schemaProvider ?? throw new ArgumentNullException(nameof(schemaProvider));
    }

    /// <inheritdoc />
    public SchemaValidationResult Validate(string data, string schemaName)
    {
        var schema = _schemaProvider.GetSchema(schemaName);
        if (schema == null)
            return SchemaValidationResult.Invalid([$"Schema '{schemaName}' not found."]);

        try
        {
            // Parse the data to ensure it's valid JSON
            using var dataDoc = JsonDocument.Parse(data);
            using var schemaDoc = JsonDocument.Parse(schema);

            var errors = new List<string>();
            ValidateElement(dataDoc.RootElement, schemaDoc.RootElement, "$", errors);

            return errors.Count == 0
                ? SchemaValidationResult.Valid()
                : SchemaValidationResult.Invalid(errors);
        }
        catch (JsonException ex)
        {
            return SchemaValidationResult.Invalid([$"Invalid JSON: {ex.Message}"]);
        }
    }

    private static void ValidateElement(JsonElement data, JsonElement schema, string path, List<string> errors)
    {
        // Check type
        if (schema.TryGetProperty("type", out var typeEl))
        {
            var expectedType = typeEl.GetString();
            var actualKind = data.ValueKind;
            var typeMatch = expectedType switch
            {
                "object" => actualKind == JsonValueKind.Object,
                "array" => actualKind == JsonValueKind.Array,
                "string" => actualKind == JsonValueKind.String,
                "number" or "integer" => actualKind == JsonValueKind.Number,
                "boolean" => actualKind is JsonValueKind.True or JsonValueKind.False,
                "null" => actualKind == JsonValueKind.Null,
                _ => true
            };

            if (!typeMatch)
                errors.Add($"{path}: expected type '{expectedType}' but got '{actualKind}'.");
        }

        // Check required fields
        if (schema.TryGetProperty("required", out var requiredEl) && data.ValueKind == JsonValueKind.Object)
        {
            foreach (var req in requiredEl.EnumerateArray())
            {
                var fieldName = req.GetString()!;
                if (!data.TryGetProperty(fieldName, out _))
                    errors.Add($"{path}: missing required field '{fieldName}'.");
            }
        }

        // Validate properties
        if (schema.TryGetProperty("properties", out var propsEl) && data.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in propsEl.EnumerateObject())
            {
                if (data.TryGetProperty(prop.Name, out var dataProp))
                    ValidateElement(dataProp, prop.Value, $"{path}.{prop.Name}", errors);
            }
        }
    }
}
