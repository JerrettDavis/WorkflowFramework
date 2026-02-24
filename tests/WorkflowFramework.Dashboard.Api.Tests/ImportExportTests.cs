using System.Text.Json;
using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Services;
using WorkflowFramework.Serialization;
using Xunit;

namespace WorkflowFramework.Dashboard.Api.Tests;

public class ImportExportTests
{
    [Fact]
    public void ExportDto_RoundTrip_Json()
    {
        var export = new WorkflowExportDto
        {
            FormatVersion = "1.0",
            Name = "Test Workflow",
            Description = "A test",
            Tags = ["tag1", "tag2"],
            Definition = new WorkflowDefinitionDto
            {
                Name = "Test Workflow",
                Steps =
                [
                    new StepDefinitionDto { Name = "Step1", Type = "Action" }
                ]
            }
        };

        var json = JsonSerializer.Serialize(export);
        var deserialized = JsonSerializer.Deserialize<WorkflowExportDto>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be("Test Workflow");
        deserialized.Tags.Should().HaveCount(2);
        deserialized.Definition.Steps.Should().HaveCount(1);
        deserialized.FormatVersion.Should().Be("1.0");
    }

    [Fact]
    public void ExportDto_YamlExport_ProducesValidYaml()
    {
        var definition = new WorkflowDefinitionDto
        {
            Name = "YAML Test",
            Steps =
            [
                new StepDefinitionDto { Name = "Step1", Type = "Action" },
                new StepDefinitionDto { Name = "Step2", Type = "Delay", DelaySeconds = 5 }
            ]
        };

        var yaml = YamlWriter.Write(definition);

        yaml.Should().Contain("name: YAML Test");
        yaml.Should().Contain("Step1");
        yaml.Should().Contain("Step2");
        yaml.Should().Contain("delaySeconds: 5");
    }

    [Fact]
    public void Import_ValidationFailure_ReportsErrors()
    {
        var validator = new WorkflowValidator();
        var definition = new WorkflowDefinitionDto
        {
            Name = "", // Invalid: empty name
            Steps = [] // Invalid: no steps
        };

        var result = validator.Validate(definition);

        result.IsValid.Should().BeFalse();
        result.ErrorCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Import_ValidDefinition_Succeeds()
    {
        var validator = new WorkflowValidator();
        var definition = new WorkflowDefinitionDto
        {
            Name = "Valid Workflow",
            Steps =
            [
                new StepDefinitionDto { Name = "Step1", Type = "Action" }
            ]
        };

        var result = validator.Validate(definition);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void BulkImport_MultipleWorkflows_AllValidated()
    {
        var validator = new WorkflowValidator();
        var exports = new List<WorkflowExportDto>
        {
            new()
            {
                Name = "Workflow1",
                Definition = new WorkflowDefinitionDto
                {
                    Name = "Workflow1",
                    Steps = [new StepDefinitionDto { Name = "S1", Type = "Action" }]
                }
            },
            new()
            {
                Name = "Workflow2",
                Definition = new WorkflowDefinitionDto
                {
                    Name = "", // Invalid
                    Steps = []
                }
            }
        };

        var validResults = exports
            .Select(e => (e.Name, Result: validator.Validate(e.Definition)))
            .ToList();

        validResults[0].Result.IsValid.Should().BeTrue();
        validResults[1].Result.IsValid.Should().BeFalse();
    }
}
