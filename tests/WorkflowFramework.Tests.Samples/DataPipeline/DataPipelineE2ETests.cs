using FluentAssertions;
using Xunit;

namespace WorkflowFramework.Tests.Samples.DataPipeline;

public class DataRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class DataPipelineE2ETests
{
    private static Func<string, CancellationToken, Task<string>> BuildPipeline()
    {
        return global::WorkflowFramework.Pipeline.Pipeline.Create<string>()
            .Pipe<List<DataRecord>>((csv, ct) =>
            {
                var records = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries)
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
                    })
                    .ToList();
                return Task.FromResult(records);
            })
            .Pipe<List<DataRecord>>((records, ct) =>
            {
                var filtered = records.Where(r => r.Value > 10).ToList();
                return Task.FromResult(filtered);
            })
            .Pipe<List<DataRecord>>((records, ct) =>
            {
                foreach (var r in records)
                    r.Value *= 1.1m;
                return Task.FromResult(records);
            })
            .Pipe<string>((records, ct) =>
            {
                var output = "Id,Name,Value\n" +
                    string.Join("\n", records.Select(r => $"{r.Id},{r.Name},{r.Value:F2}"));
                return Task.FromResult(output);
            })
            .Build();
    }

    [Fact]
    public async Task Pipeline_ParsesCSV_ProducesFilteredOutput()
    {
        var pipeline = BuildPipeline();
        var input = "Id,Name,Value\n1,Widget,25.00\n2,Gadget,5.00\n3,Doohickey,50.00";

        var result = await pipeline(input, CancellationToken.None);

        result.Should().NotContain("Gadget");
        result.Should().Contain("Widget");
        result.Should().Contain("27.50");
        result.Should().Contain("Doohickey");
        result.Should().Contain("55.00");
    }

    [Fact]
    public async Task Pipeline_EmptyCSV_HandlesGracefully()
    {
        var pipeline = BuildPipeline();
        var input = "Id,Name,Value";

        var result = await pipeline(input, CancellationToken.None);

        result.Should().Be("Id,Name,Value\n");
    }

    [Fact]
    public async Task Pipeline_SingleRecord_ProcessesCorrectly()
    {
        var pipeline = BuildPipeline();
        var input = "Id,Name,Value\n1,Widget,25.00";

        var result = await pipeline(input, CancellationToken.None);

        result.Should().Contain("Widget");
        result.Should().Contain("27.50");
    }
}
