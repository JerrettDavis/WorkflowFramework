using FluentAssertions;
using NSubstitute;
using WorkflowFramework.Extensions.DataMapping.Abstractions;
using WorkflowFramework.Extensions.DataMapping.Steps;
using Xunit;

namespace WorkflowFramework.Tests.DataMapping;

public class DataMapStepTests
{
    [Fact]
    public void Constructor_NullMapper_Throws()
    {
        var act = () => new DataMapStep(null!, new DataMappingProfile());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullProfile_Throws()
    {
        var mapper = Substitute.For<IDataMapper>();
        var act = () => new DataMapStep(mapper, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_MissingSource_Throws()
    {
        var mapper = Substitute.For<IDataMapper>();
        var step = new DataMapStep(mapper, new DataMappingProfile());
        var context = new WorkflowContext();
        var act = () => step.ExecuteAsync(context);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*source*");
    }
}

public class FormatConvertStepTests
{
    [Fact]
    public void Constructor_NullConverter_Throws()
    {
        var act = () => new FormatConvertStep(null!, DataFormat.Json, DataFormat.Xml);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_MissingSource_Throws()
    {
        var converter = Substitute.For<IFormatConverter>();
        var step = new FormatConvertStep(converter, DataFormat.Json, DataFormat.Xml);
        var context = new WorkflowContext();
        var act = () => step.ExecuteAsync(context);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteAsync_ConvertsAndStores()
    {
        var converter = Substitute.For<IFormatConverter>();
        converter.Convert("{}", DataFormat.Json, DataFormat.Xml).Returns("<root/>");
        var step = new FormatConvertStep(converter, DataFormat.Json, DataFormat.Xml);
        var context = new WorkflowContext();
        context.Properties[DataMapStep.SourceKey] = "{}";
        await step.ExecuteAsync(context);
        context.Properties[DataMapStep.DestinationKey].Should().Be("<root/>");
    }
}

public class SchemaValidateStepTests
{
    [Fact]
    public void Constructor_NullValidator_Throws()
    {
        var act = () => new SchemaValidateStep(null!, "schema");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_MissingData_Throws()
    {
        var validator = Substitute.For<ISchemaValidator>();
        var step = new SchemaValidateStep(validator, "test");
        var context = new WorkflowContext();
        var act = () => step.ExecuteAsync(context);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteAsync_ValidData_Succeeds()
    {
        var validator = Substitute.For<ISchemaValidator>();
        validator.Validate(Arg.Any<string>(), "test").Returns(SchemaValidationResult.Valid());
        var step = new SchemaValidateStep(validator, "test");
        var context = new WorkflowContext();
        context.Properties[DataMapStep.SourceKey] = "{}";
        await step.ExecuteAsync(context); // Should not throw
    }

    [Fact]
    public async Task ExecuteAsync_InvalidData_Throws()
    {
        var validator = Substitute.For<ISchemaValidator>();
        validator.Validate(Arg.Any<string>(), "test").Returns(SchemaValidationResult.Invalid(new[] { "bad field" }));
        var step = new SchemaValidateStep(validator, "test");
        var context = new WorkflowContext();
        context.Properties[DataMapStep.SourceKey] = "{}";
        var act = () => step.ExecuteAsync(context);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*bad field*");
    }

    [Fact]
    public async Task ExecuteAsync_ValidatesDestination()
    {
        var validator = Substitute.For<ISchemaValidator>();
        validator.Validate(Arg.Any<string>(), "test").Returns(SchemaValidationResult.Valid());
        var step = new SchemaValidateStep(validator, "test", validateDestination: true);
        var context = new WorkflowContext();
        context.Properties[DataMapStep.DestinationKey] = "{}";
        await step.ExecuteAsync(context); // Should not throw
    }
}
