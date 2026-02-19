using Xunit;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using WorkflowFramework.Extensions.DataMapping.Abstractions;
using WorkflowFramework.Extensions.DataMapping.Engine;
using WorkflowFramework.Extensions.DataMapping.Readers;
using WorkflowFramework.Extensions.DataMapping.Transformers;
using WorkflowFramework.Extensions.DataMapping.Writers;

namespace WorkflowFramework.Tests.DataMapping;

public class DataMapperTests
{
    private readonly DataMapper _mapper;

    public DataMapperTests()
    {
        var registry = new FieldTransformerRegistry([
            new ToUpperTransformer(),
            new TrimTransformer(),
            new DefaultValueTransformer()
        ]);

        _mapper = new DataMapper(
            registry,
            readers: [new JsonSourceReader(), new DictionarySourceReader()],
            writers: [new JsonDestinationWriter(), new DictionaryDestinationWriter()]
        );
    }

    [Fact]
    public async Task Map_JsonToJson_MapsFieldsCorrectly()
    {
        var profile = new DataMappingProfile
        {
            Name = "test",
            Mappings =
            {
                new FieldMapping("$.name", "$.fullName", [new TransformerRef("toUpper")]),
                new FieldMapping("$.age", "$.years")
            }
        };

        var json = """{"name": "John Doe", "age": 30}""";
        using var doc = JsonDocument.Parse(json);
        var dest = new JsonObject();

        var result = await _mapper.MapAsync(profile, doc.RootElement, dest);

        result.IsSuccess.Should().BeTrue();
        result.MappedFieldCount.Should().Be(2);
        dest["fullName"]!.GetValue<string>().Should().Be("JOHN DOE");
        dest["years"]!.GetValue<string>().Should().Be("30");
    }

    [Fact]
    public async Task Map_WithDefaults_AppliesDefaultWhenNull()
    {
        var profile = new DataMappingProfile
        {
            Name = "test",
            Mappings = { new FieldMapping("$.missing", "$.status") },
            Defaults = { ["$.status"] = "Unknown" }
        };

        var json = """{"name": "test"}""";
        using var doc = JsonDocument.Parse(json);
        var dest = new JsonObject();

        var result = await _mapper.MapAsync(profile, doc.RootElement, dest);

        result.IsSuccess.Should().BeTrue();
        dest["status"]!.GetValue<string>().Should().Be("Unknown");
    }

    [Fact]
    public async Task Map_DictionaryToDict_Works()
    {
        var profile = new DataMappingProfile
        {
            Name = "dictTest",
            Mappings =
            {
                new FieldMapping("firstName", "name", [new TransformerRef("trim")])
            }
        };

        var source = new Dictionary<string, object?> { ["firstName"] = "  Alice  " };
        var dest = new Dictionary<string, object?>();

        var result = await _mapper.MapAsync(profile, source, dest);

        result.IsSuccess.Should().BeTrue();
        dest["name"].Should().Be("Alice");
    }

    [Fact]
    public async Task Map_NestedJson_ReadsNestedPaths()
    {
        var profile = new DataMappingProfile
        {
            Name = "nested",
            Mappings =
            {
                new FieldMapping("$.customer.name", "$.name"),
                new FieldMapping("$.items[0].id", "$.firstItemId")
            }
        };

        var json = """{"customer":{"name":"Bob"},"items":[{"id":"42"},{"id":"43"}]}""";
        using var doc = JsonDocument.Parse(json);
        var dest = new JsonObject();

        var result = await _mapper.MapAsync(profile, doc.RootElement, dest);

        result.IsSuccess.Should().BeTrue();
        dest["name"]!.GetValue<string>().Should().Be("Bob");
        dest["firstItemId"]!.GetValue<string>().Should().Be("42");
    }

    [Fact]
    public async Task Map_TransformerChain_AppliesInOrder()
    {
        var profile = new DataMappingProfile
        {
            Name = "chain",
            Mappings =
            {
                new FieldMapping("$.val", "$.result", [
                    new TransformerRef("trim"),
                    new TransformerRef("toUpper")
                ])
            }
        };

        var json = """{"val": "  hello world  "}""";
        using var doc = JsonDocument.Parse(json);
        var dest = new JsonObject();

        var result = await _mapper.MapAsync(profile, doc.RootElement, dest);

        dest["result"]!.GetValue<string>().Should().Be("HELLO WORLD");
    }
}
