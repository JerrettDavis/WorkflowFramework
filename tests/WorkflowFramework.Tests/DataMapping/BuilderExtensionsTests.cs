using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WorkflowFramework.Extensions.DataMapping;
using WorkflowFramework.Extensions.DataMapping.Abstractions;
using WorkflowFramework.Extensions.DataMapping.Batch;
using WorkflowFramework.Extensions.DataMapping.Builder;
using WorkflowFramework.Extensions.DataMapping.Engine;
using WorkflowFramework.Extensions.DataMapping.Transformers;
using Xunit;

namespace WorkflowFramework.Tests.DataMapping;

public class BuilderExtensionsTests
{
    private static DataMapper CreateMapper()
    {
        var registry = new FieldTransformerRegistry(Array.Empty<IFieldTransformer>());
        return new DataMapper(registry, Array.Empty<object>(), Array.Empty<object>());
    }

    [Fact]
    public void MapData_WithConfigure_BuildsProfileAndAddsStep()
    {
        var workflow = Workflow.Create("test")
            .MapData(CreateMapper(), p => p.Named("Test").Map("a", "b"))
            .Build();

        workflow.Steps.Should().HaveCount(1);
        workflow.Steps[0].Name.Should().Be("DataMapStep");
    }

    [Fact]
    public void MapData_WithProfile_AddsStep()
    {
        var profile = new DataMappingProfile { Name = "p" };
        var workflow = Workflow.Create("test")
            .MapData(CreateMapper(), profile)
            .Build();

        workflow.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void ConvertFormat_AddsFormatConvertStep()
    {
        var converter = new WorkflowFramework.Extensions.DataMapping.Formats.Converters.FormatConverter();
        var workflow = Workflow.Create("test")
            .ConvertFormat(converter, DataFormat.Json, DataFormat.Xml)
            .Build();

        workflow.Steps.Should().HaveCount(1);
        workflow.Steps[0].Name.Should().Be("FormatConvertStep");
    }

    [Fact]
    public void ValidateSchema_AddsSchemaValidateStep()
    {
        var validator = new WorkflowFramework.Extensions.DataMapping.Schema.Validators.JsonSchemaValidator(
            new WorkflowFramework.Extensions.DataMapping.Schema.Abstractions.SchemaRegistry());
        var workflow = Workflow.Create("test")
            .ValidateSchema(validator, "mySchema", true)
            .Build();

        workflow.Steps.Should().HaveCount(1);
        workflow.Steps[0].Name.Should().Be("SchemaValidateStep");
    }

    [Fact]
    public void BatchProcess_AddsStep_WithDefaults()
    {
        var workflow = Workflow.Create("test")
            .BatchProcess((batch, ctx) => Task.CompletedTask)
            .Build();

        workflow.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void BatchProcess_AddsStep_WithOptions()
    {
        var workflow = Workflow.Create("test")
            .BatchProcess((batch, ctx) => Task.CompletedTask, opts => opts.BatchSize = 50)
            .Build();

        workflow.Steps.Should().HaveCount(1);
    }
}

public class DataMappingProfileBuilderTests
{
    [Fact]
    public void Named_SetsName()
    {
        var profile = new DataMappingProfileBuilder()
            .Named("MyProfile")
            .Build();

        profile.Name.Should().Be("MyProfile");
    }

    [Fact]
    public void Map_AddsFieldMapping()
    {
        var profile = new DataMappingProfileBuilder()
            .Map("src", "dst")
            .Build();

        profile.Mappings.Should().HaveCount(1);
        profile.Mappings[0].SourcePath.Should().Be("src");
        profile.Mappings[0].DestinationPath.Should().Be("dst");
    }

    [Fact]
    public void Map_WithTransformers_IncludesThem()
    {
        var t = new TransformerRef("upper");
        var profile = new DataMappingProfileBuilder()
            .Map("a", "b", t)
            .Build();

        profile.Mappings[0].Transformers.Should().Contain(t);
    }

    [Fact]
    public void Map_WithoutTransformers_NullTransformers()
    {
        var profile = new DataMappingProfileBuilder()
            .Map("a", "b")
            .Build();

        profile.Mappings[0].Transformers.Should().BeNull();
    }

    [Fact]
    public void WithDefault_SetsDefault()
    {
        var profile = new DataMappingProfileBuilder()
            .WithDefault("path", "val")
            .Build();

        profile.Defaults["path"].Should().Be("val");
    }

    [Fact]
    public void FluentChaining_Works()
    {
        var profile = new DataMappingProfileBuilder()
            .Named("Chain")
            .Map("a", "b")
            .Map("c", "d")
            .WithDefault("b", "x")
            .Build();

        profile.Name.Should().Be("Chain");
        profile.Mappings.Should().HaveCount(2);
        profile.Defaults.Should().ContainKey("b");
    }
}

public class DataMappingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDataMapping_RegistersAllServices()
    {
        var services = new ServiceCollection();
        var result = services.AddDataMapping();

        result.Should().BeSameAs(services);

        var sp = services.BuildServiceProvider();
        sp.GetService<IFieldTransformerRegistry>().Should().NotBeNull();
        sp.GetService<IDataMapper>().Should().NotBeNull();
        sp.GetService<IDataBatcher>().Should().NotBeNull();
    }

    [Fact]
    public void AddDataMapping_RegistersBuiltInTransformers()
    {
        var services = new ServiceCollection();
        services.AddDataMapping();

        var sp = services.BuildServiceProvider();
        var transformers = sp.GetServices<IFieldTransformer>().ToList();
        transformers.Should().HaveCountGreaterThan(8);
        transformers.Should().Contain(t => t is ToUpperTransformer);
        transformers.Should().Contain(t => t is ToLowerTransformer);
        transformers.Should().Contain(t => t is TrimTransformer);
    }
}
