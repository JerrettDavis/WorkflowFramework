using WorkflowFramework;
using WorkflowFramework.Pipeline;

// === Data Pipeline using Typed Pipeline Builder ===

Console.WriteLine("=== ETL Data Pipeline ===");
Console.WriteLine();

// Build a typed pipeline: string CSV → parsed records → filtered → transformed → output
var pipeline = Pipeline.Create<string>()
    .Pipe<List<DataRecord>>((csv, ct) =>
    {
        var records = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1) // Skip header
            .Select(line =>
            {
                var parts = line.Split(',');
                return new DataRecord
                {
                    Id = int.Parse(parts[0].Trim()),
                    Name = parts[1].Trim(),
                    Value = decimal.Parse(parts[2].Trim())
                };
            })
            .ToList();
        Console.WriteLine($"  ✓ Parsed {records.Count} records");
        return Task.FromResult(records);
    })
    .Pipe<List<DataRecord>>((records, ct) =>
    {
        var filtered = records.Where(r => r.Value > 10).ToList();
        Console.WriteLine($"  ✓ Filtered to {filtered.Count} records (value > 10)");
        return Task.FromResult(filtered);
    })
    .Pipe<List<DataRecord>>((records, ct) =>
    {
        foreach (var r in records)
            r.Value *= 1.1m; // Apply 10% markup
        Console.WriteLine($"  ✓ Applied 10% markup to {records.Count} records");
        return Task.FromResult(records);
    })
    .Pipe<string>((records, ct) =>
    {
        var output = "Id,Name,Value\n" +
            string.Join("\n", records.Select(r => $"{r.Id},{r.Name},{r.Value:F2}"));
        Console.WriteLine($"  ✓ Generated output CSV");
        return Task.FromResult(output);
    })
    .Build();

var inputCsv = @"Id,Name,Value
1,Widget,25.00
2,Gadget,5.00
3,Doohickey,50.00
4,Thingamajig,8.00
5,Whatchamacallit,100.00";

var result = await pipeline(inputCsv, CancellationToken.None);
Console.WriteLine();
Console.WriteLine("Result:");
Console.WriteLine(result);

// === Also demonstrate workflow-based ETL ===

Console.WriteLine();
Console.WriteLine("=== Workflow-based ETL ===");

var etlWorkflow = Workflow.Create<EtlData>("DataPipeline")
    .Step("Extract", async ctx =>
    {
        ctx.Data.RawRecords = ctx.Data.SourceData.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Skip(1)
            .Select(line =>
            {
                var parts = line.Split(',');
                return new DataRecord
                {
                    Id = int.Parse(parts[0].Trim()),
                    Name = parts[1].Trim(),
                    Value = decimal.Parse(parts[2].Trim())
                };
            }).ToList();
        Console.WriteLine($"  ✓ Extracted {ctx.Data.RawRecords.Count} records");
        await Task.CompletedTask;
    })
    .Step("Transform", ctx =>
    {
        ctx.Data.TransformedRecords = ctx.Data.RawRecords
            .Where(r => r.Value > 10)
            .Select(r => new DataRecord { Id = r.Id, Name = r.Name.ToUpper(), Value = r.Value * 1.1m })
            .ToList();
        Console.WriteLine($"  ✓ Transformed {ctx.Data.TransformedRecords.Count} records");
        return Task.CompletedTask;
    })
    .Step("Load", ctx =>
    {
        ctx.Data.OutputCsv = "Id,Name,Value\n" +
            string.Join("\n", ctx.Data.TransformedRecords.Select(r => $"{r.Id},{r.Name},{r.Value:F2}"));
        Console.WriteLine($"  ✓ Loaded output");
        return Task.CompletedTask;
    })
    .Build();

var etlData = new EtlData { SourceData = inputCsv };
var etlContext = new WorkflowContext<EtlData>(etlData);
var etlResult = await etlWorkflow.ExecuteAsync(etlContext);

Console.WriteLine($"Status: {etlResult.Status}");
Console.WriteLine(etlResult.Data.OutputCsv);

// === Types ===

public class DataRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class EtlData
{
    public string SourceData { get; set; } = string.Empty;
    public List<DataRecord> RawRecords { get; set; } = new();
    public List<DataRecord> TransformedRecords { get; set; } = new();
    public string OutputCsv { get; set; } = string.Empty;
}
