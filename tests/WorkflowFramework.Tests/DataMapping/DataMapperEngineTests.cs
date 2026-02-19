using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.DataMapping.Abstractions;
using WorkflowFramework.Extensions.DataMapping.Engine;
using Xunit;

namespace WorkflowFramework.Tests.DataMapping;

public class DataMapperEngineTests
{
    [Fact]
    public void Constructor_NullRegistry_Throws()
    {
        var act = () => new DataMapper(null!, Array.Empty<object>(), Array.Empty<object>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullReaders_Throws()
    {
        var registry = Substitute.For<IFieldTransformerRegistry>();
        var act = () => new DataMapper(registry, null!, Array.Empty<object>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullWriters_Throws()
    {
        var registry = Substitute.For<IFieldTransformerRegistry>();
        var act = () => new DataMapper(registry, Array.Empty<object>(), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task MapAsync_NullProfile_Throws()
    {
        var registry = Substitute.For<IFieldTransformerRegistry>();
        var mapper = new DataMapper(registry, Array.Empty<object>(), Array.Empty<object>());
        var act = () => mapper.MapAsync<string, string>(null!, "src", "dst");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task MapAsync_NullSource_Throws()
    {
        var registry = Substitute.For<IFieldTransformerRegistry>();
        var mapper = new DataMapper(registry, Array.Empty<object>(), Array.Empty<object>());
        var profile = new DataMappingProfile { Name = "test" };
        var act = () => mapper.MapAsync<string, string>(profile, null!, "dst");
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task MapAsync_EmptyMappings_ReturnsSuccess()
    {
        var registry = Substitute.For<IFieldTransformerRegistry>();
        var mapper = new DataMapper(registry, Array.Empty<object>(), Array.Empty<object>());
        var profile = new DataMappingProfile { Name = "empty" };
        var result = await mapper.MapAsync(profile, "source", "dest");
        result.IsSuccess.Should().BeTrue();
        result.MappedFieldCount.Should().Be(0);
        result.TotalFieldCount.Should().Be(0);
    }

    [Fact]
    public async Task MapAsync_NoMatchingReader_RecordsError()
    {
        var registry = Substitute.For<IFieldTransformerRegistry>();
        registry.ApplyAll(Arg.Any<string?>(), Arg.Any<IEnumerable<TransformerRef>?>()).Returns(x => x.Arg<string?>());
        var mapper = new DataMapper(registry, Array.Empty<object>(), Array.Empty<object>());
        var profile = new DataMappingProfile { Name = "test" };
        profile.Mappings.Add(new FieldMapping("src.path", "dst.path"));
        var result = await mapper.MapAsync(profile, "source", "dest");
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void DataMappingResult_Success()
    {
        var result = DataMappingResult.Success(5, 5);
        result.IsSuccess.Should().BeTrue();
        result.MappedFieldCount.Should().Be(5);
        result.TotalFieldCount.Should().Be(5);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void DataMappingResult_Failure()
    {
        var result = DataMappingResult.Failure(new[] { "error1" }, 1, 3);
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain("error1");
        result.MappedFieldCount.Should().Be(1);
    }

    [Fact]
    public void DataMappingProfile_DefaultValues()
    {
        var profile = new DataMappingProfile();
        profile.Name.Should().BeEmpty();
        profile.Mappings.Should().BeEmpty();
        profile.Defaults.Should().BeEmpty();
    }

    [Fact]
    public void FieldMapping_Properties()
    {
        var mapping = new FieldMapping("src", "dst", new[] { new TransformerRef("toUpper") });
        mapping.SourcePath.Should().Be("src");
        mapping.DestinationPath.Should().Be("dst");
        mapping.Transformers.Should().HaveCount(1);
    }

    [Fact]
    public void TransformerRef_Properties()
    {
        var tr = new TransformerRef("trim", new Dictionary<string, string?> { ["key"] = "val" });
        tr.Name.Should().Be("trim");
        tr.Args.Should().ContainKey("key");
    }

    [Theory]
    [InlineData(DataFormat.Json)]
    [InlineData(DataFormat.Xml)]
    [InlineData(DataFormat.Csv)]
    [InlineData(DataFormat.Yaml)]
    [InlineData(DataFormat.Dictionary)]
    public void DataFormat_EnumValues(DataFormat format)
    {
        Enum.IsDefined(typeof(DataFormat), format).Should().BeTrue();
    }
}
