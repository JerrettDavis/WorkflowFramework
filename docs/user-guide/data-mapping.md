# Data Mapping & Transformation

The `Extensions.DataMapping` package provides a declarative data mapping engine with field-level transformations, format conversion, and schema validation.

## Installation

```bash
dotnet add package WorkflowFramework.Extensions.DataMapping
dotnet add package WorkflowFramework.Extensions.DataMapping.Formats  # JSON/XML/CSV converters
dotnet add package WorkflowFramework.Extensions.DataMapping.Schema   # schema validation
```

## Core Concepts

### DataMappingProfile

A profile defines how fields map from source to destination:

```csharp
using WorkflowFramework.Extensions.DataMapping.Abstractions;

var profile = new DataMappingProfile
{
    Name = "OrderToInvoice"
};

profile.Mappings.Add(new FieldMapping("Customer.Name", "BillTo.FullName"));
profile.Mappings.Add(new FieldMapping("Customer.Email", "BillTo.Email"));
profile.Mappings.Add(new FieldMapping("Total", "Amount", new[]
{
    new TransformerRef("round", new Dictionary<string, string?> { ["decimals"] = "2" })
}));

profile.Defaults["BillTo.Currency"] = "USD";
```

### IDataMapper

```csharp
var mapper = new DataMapper(transformerRegistry);
var result = await mapper.MapAsync(profile, sourceObj, destObj);

if (result.IsSuccess)
    Console.WriteLine($"Mapped {result.MappedFieldCount}/{result.TotalFieldCount} fields");
else
    Console.WriteLine($"Errors: {string.Join(", ", result.Errors)}");
```

## Field Transformers

Register custom transformers via `IFieldTransformerRegistry`:

```csharp
var registry = new FieldTransformerRegistry();
registry.Register("uppercase", (value, args) =>
    Task.FromResult<object?>(value?.ToString()?.ToUpperInvariant()));
registry.Register("round", (value, args) =>
{
    var decimals = int.Parse(args?["decimals"] ?? "2");
    return Task.FromResult<object?>(Math.Round(Convert.ToDecimal(value), decimals));
});
```

## Source Readers & Destination Writers

The `ISourceReader` and `IDestinationWriter` abstractions support:

- **JSON** — read/write JSON documents by path
- **XML** — XPath-based reading/writing
- **Dictionary** — flat key-value pairs
- **Object** — .NET object property access via reflection

## Format Conversion

The `DataMapping.Formats` package provides converters between JSON, XML, and CSV:

```csharp
// Convert JSON to XML
var xml = await FormatConverter.ConvertAsync(jsonString, DataFormat.Json, DataFormat.Xml);
```

## Schema Validation

Validate data against JSON Schema or XML Schema before or after mapping:

```csharp
// JSON Schema validation
var validator = new JsonSchemaValidator(schemaJson);
var errors = validator.Validate(dataJson);
```

## Batch Processing

`BatchProcessStep` maps collections of items in bulk:

```csharp
using WorkflowFramework.Extensions.DataMapping.Batch;

var batchStep = new BatchProcessStep(mapper, profile, "InputItems", "OutputItems");
```

## Builder Extensions

```csharp
using WorkflowFramework.Extensions.DataMapping.Builder;

var workflow = new WorkflowBuilder()
    .MapData(profile, sourceReader, destinationWriter)
    .Build();
```

> [!TIP]
> Combine data mapping with [integration patterns](integration-patterns.md) like Message Translator and Content Enricher for powerful ETL workflows.
