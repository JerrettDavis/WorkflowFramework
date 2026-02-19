using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using WorkflowFramework.Extensions.DataMapping.Abstractions;
using WorkflowFramework.Extensions.DataMapping.Engine;
using WorkflowFramework.Extensions.DataMapping.Readers;
using WorkflowFramework.Extensions.DataMapping.Transformers;
using WorkflowFramework.Extensions.DataMapping.Writers;
using Xunit;

namespace WorkflowFramework.Tests.DataMapping;

public class DataMapperEngineExtendedTests
{
    [Fact]
    public async Task MapAsync_NullDestination_Throws()
    {
        var registry = new FieldTransformerRegistry();
        var mapper = new DataMapper(registry, Array.Empty<object>(), Array.Empty<object>());
        var profile = new DataMappingProfile();
        var act = () => mapper.MapAsync<string, string>(profile, "src", null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task MapAsync_WithMatchingReaderAndWriter_MapsSuccessfully()
    {
        var registry = new FieldTransformerRegistry([new ToUpperTransformer()]);
        var readers = new object[] { new JsonSourceReader() };
        var writers = new object[] { new JsonDestinationWriter() };
        var mapper = new DataMapper(registry, readers, writers);

        var profile = new DataMappingProfile();
        profile.Mappings.Add(new FieldMapping("$.name", "$.fullName", [new TransformerRef("toUpper")]));

        using var doc = JsonDocument.Parse("""{"name":"alice"}""");
        var dest = new JsonObject();
        var result = await mapper.MapAsync(profile, doc.RootElement, dest);

        result.IsSuccess.Should().BeTrue();
        result.MappedFieldCount.Should().Be(1);
        dest["fullName"]!.GetValue<string>().Should().Be("ALICE");
    }

    [Fact]
    public async Task MapAsync_AppliesDefaultWhenSourceNull()
    {
        var registry = new FieldTransformerRegistry();
        var readers = new object[] { new JsonSourceReader() };
        var writers = new object[] { new JsonDestinationWriter() };
        var mapper = new DataMapper(registry, readers, writers);

        var profile = new DataMappingProfile();
        profile.Mappings.Add(new FieldMapping("$.missing", "$.status"));
        profile.Defaults["$.status"] = "unknown";

        using var doc = JsonDocument.Parse("""{"name":"test"}""");
        var dest = new JsonObject();
        var result = await mapper.MapAsync(profile, doc.RootElement, dest);

        result.IsSuccess.Should().BeTrue();
        dest["status"]!.GetValue<string>().Should().Be("unknown");
    }

    [Fact]
    public async Task MapAsync_CancellationToken_Respected()
    {
        var registry = new FieldTransformerRegistry();
        var readers = new object[] { new JsonSourceReader() };
        var writers = new object[] { new JsonDestinationWriter() };
        var mapper = new DataMapper(registry, readers, writers);

        var profile = new DataMappingProfile();
        profile.Mappings.Add(new FieldMapping("$.a", "$.b"));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var doc = JsonDocument.Parse("""{"a":"1"}""");
        var dest = new JsonObject();
        var act = () => mapper.MapAsync(profile, doc.RootElement, dest, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task MapAsync_NoWriter_RecordsError()
    {
        var registry = new FieldTransformerRegistry();
        var readers = new object[] { new JsonSourceReader() };
        var mapper = new DataMapper(registry, readers, Array.Empty<object>());

        var profile = new DataMappingProfile();
        profile.Mappings.Add(new FieldMapping("$.name", "$.name"));

        using var doc = JsonDocument.Parse("""{"name":"test"}""");
        var dest = new JsonObject();
        var result = await mapper.MapAsync(profile, doc.RootElement, dest);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("No writer"));
    }
}

public class FieldTransformerRegistryExtendedTests
{
    [Fact]
    public void Get_EmptyName_ReturnsNull()
    {
        var registry = new FieldTransformerRegistry();
        registry.Get("").Should().BeNull();
    }

    [Fact]
    public void Get_NullName_ReturnsNull()
    {
        var registry = new FieldTransformerRegistry();
        registry.Get(null!).Should().BeNull();
    }

    [Fact]
    public void Get_CaseInsensitive()
    {
        var registry = new FieldTransformerRegistry([new ToUpperTransformer()]);
        registry.Get("TOUPPER").Should().NotBeNull();
        registry.Get("toupper").Should().NotBeNull();
    }

    [Fact]
    public void Register_NullTransformer_Throws()
    {
        var registry = new FieldTransformerRegistry();
        var act = () => registry.Register(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ApplyAll_UnknownTransformer_SkipsIt()
    {
        var registry = new FieldTransformerRegistry();
        var chain = new[] { new TransformerRef("nonexistent") };
        registry.ApplyAll("hello", chain).Should().Be("hello");
    }

    [Fact]
    public void ApplyAll_TransformerThrows_PassesThrough()
    {
        var registry = new FieldTransformerRegistry([new ThrowingTransformer()]);
        var chain = new[] { new TransformerRef("throws") };
        registry.ApplyAll("hello", chain).Should().Be("hello");
    }

    [Fact]
    public void Constructor_NullTransformers_CreatesEmptyRegistry()
    {
        var registry = new FieldTransformerRegistry(null);
        registry.RegisteredNames.Should().BeEmpty();
    }

    private class ThrowingTransformer : IFieldTransformer
    {
        public string Name => "throws";
        public string? Transform(string? input, IReadOnlyDictionary<string, string?>? args = null) => throw new Exception("boom");
    }
}
