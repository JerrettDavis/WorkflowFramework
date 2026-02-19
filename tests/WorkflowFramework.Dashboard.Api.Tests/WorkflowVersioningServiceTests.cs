using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Services;
using WorkflowFramework.Serialization;
using Xunit;

namespace WorkflowFramework.Dashboard.Api.Tests;

public class WorkflowVersioningServiceTests
{
    private readonly WorkflowVersioningService _sut = new();

    private static SavedWorkflowDefinition CreateWorkflow(string id = "wf1", string name = "Test", int stepCount = 0)
    {
        var wf = new SavedWorkflowDefinition
        {
            Id = id,
            Description = "desc",
            LastModified = DateTimeOffset.UtcNow,
            Definition = new WorkflowDefinitionDto { Name = name }
        };
        for (int i = 0; i < stepCount; i++)
            wf.Definition.Steps.Add(new StepDefinitionDto { Name = $"Step{i}", Type = "action" });
        return wf;
    }

    [Fact]
    public void CreateVersion_FirstVersion_ReturnsVersion1()
    {
        var wf = CreateWorkflow();
        var v = _sut.CreateVersion(wf);
        v.VersionNumber.Should().Be(1);
        v.ChangeSummary.Should().Be("Initial version");
    }

    [Fact]
    public void CreateVersion_SecondVersion_IncrementsNumber()
    {
        var wf = CreateWorkflow();
        _sut.CreateVersion(wf);
        wf.Definition.Steps.Add(new StepDefinitionDto { Name = "New", Type = "action" });
        var v2 = _sut.CreateVersion(wf);
        v2.VersionNumber.Should().Be(2);
        v2.ChangeSummary.Should().Contain("Added 1 step");
    }

    [Fact]
    public void GetVersions_ReturnsAllVersions()
    {
        var wf = CreateWorkflow();
        _sut.CreateVersion(wf);
        _sut.CreateVersion(wf);
        _sut.CreateVersion(wf);
        _sut.GetVersions("wf1").Should().HaveCount(3);
    }

    [Fact]
    public void GetVersion_ReturnsCorrectVersion()
    {
        var wf = CreateWorkflow();
        _sut.CreateVersion(wf);
        wf.Definition.Name = "Updated";
        _sut.CreateVersion(wf);
        var v1 = _sut.GetVersion("wf1", 1);
        v1.Should().NotBeNull();
        v1!.Snapshot.Definition.Name.Should().Be("Test");
    }

    [Fact]
    public void GetVersion_NonExistent_ReturnsNull()
    {
        _sut.GetVersion("nope", 1).Should().BeNull();
    }

    [Fact]
    public void Diff_DetectsAddedSteps()
    {
        var wf = CreateWorkflow(stepCount: 1);
        _sut.CreateVersion(wf);
        wf.Definition.Steps.Add(new StepDefinitionDto { Name = "Step1", Type = "action" });
        _sut.CreateVersion(wf);

        var diff = _sut.Diff("wf1", 1, 2);
        diff.Should().NotBeNull();
        diff!.AddedSteps.Should().HaveCount(1);
        diff.AddedSteps[0].Name.Should().Be("Step1");
    }

    [Fact]
    public void Diff_DetectsRemovedSteps()
    {
        var wf = CreateWorkflow(stepCount: 2);
        _sut.CreateVersion(wf);
        wf.Definition.Steps.RemoveAt(1);
        _sut.CreateVersion(wf);

        var diff = _sut.Diff("wf1", 1, 2);
        diff.Should().NotBeNull();
        diff!.RemovedSteps.Should().HaveCount(1);
    }

    [Fact]
    public void Diff_DetectsNameChange()
    {
        var wf = CreateWorkflow();
        _sut.CreateVersion(wf);
        wf.Definition.Name = "Renamed";
        _sut.CreateVersion(wf);

        var diff = _sut.Diff("wf1", 1, 2);
        diff!.NameChanged.Should().BeTrue();
        diff.OldName.Should().Be("Test");
        diff.NewName.Should().Be("Renamed");
    }

    [Fact]
    public void Diff_NonExistentVersion_ReturnsNull()
    {
        _sut.Diff("wf1", 1, 2).Should().BeNull();
    }
}
