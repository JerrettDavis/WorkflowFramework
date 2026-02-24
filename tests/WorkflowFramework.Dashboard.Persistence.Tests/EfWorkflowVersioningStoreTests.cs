using Xunit;
using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Persistence;
using WorkflowFramework.Serialization;

namespace WorkflowFramework.Dashboard.Persistence.Tests;

public sealed class EfWorkflowVersioningStoreTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly DashboardDbContext _db;
    private readonly EfWorkflowVersioningStore _store;

    public EfWorkflowVersioningStoreTests()
    {
        _db = _factory.CreateSeeded();
        // Create the workflow entity that versions will reference
        _db.Workflows.Add(new WorkflowFramework.Dashboard.Persistence.Entities.WorkflowEntity
        {
            Id = "wf-1", OwnerId = "system", Name = "Test Workflow"
        });
        _db.SaveChanges();
        _store = new EfWorkflowVersioningStore(_db);
    }

    private static SavedWorkflowDefinition MakeWorkflow(string id = "wf-1", string name = "Test",
        List<StepDefinitionDto>? steps = null) => new()
    {
        Id = id,
        Definition = new WorkflowDefinitionDto
        {
            Name = name,
            Steps = steps ?? [new StepDefinitionDto { Name = "Step1", Type = "Action" }]
        },
        LastModified = DateTimeOffset.UtcNow
    };

    [Fact]
    public void CreateVersion_ReturnsVersion1_ForFirstVersion()
    {
        var version = _store.CreateVersion(MakeWorkflow(), changeSummary: "Initial");
        version.VersionNumber.Should().Be(1);
        version.ChangeSummary.Should().Be("Initial");
    }

    [Fact]
    public void CreateVersion_IncrementsVersionNumber()
    {
        var wf = MakeWorkflow();
        _store.CreateVersion(wf, changeSummary: "v1");
        var v2 = _store.CreateVersion(wf, changeSummary: "v2");
        v2.VersionNumber.Should().Be(2);
    }

    [Fact]
    public void GetVersions_ReturnsAllVersions()
    {
        var wf = MakeWorkflow();
        _store.CreateVersion(wf, changeSummary: "v1");
        _store.CreateVersion(wf, changeSummary: "v2");

        var versions = _store.GetVersions("wf-1");
        versions.Should().HaveCount(2);
        versions[0].VersionNumber.Should().Be(1);
        versions[1].VersionNumber.Should().Be(2);
    }

    [Fact]
    public void GetVersion_ReturnsSpecificVersion()
    {
        var wf = MakeWorkflow();
        _store.CreateVersion(wf, changeSummary: "v1");

        var version = _store.GetVersion("wf-1", 1);
        version.Should().NotBeNull();
        version!.Snapshot.Definition.Name.Should().Be("Test");
    }

    [Fact]
    public void GetVersion_ReturnsNull_WhenNotFound()
    {
        _store.GetVersion("nonexistent", 1).Should().BeNull();
    }

    [Fact]
    public void GetVersions_ReturnsEmpty_WhenNoVersions()
    {
        _store.GetVersions("nonexistent").Should().BeEmpty();
    }

    [Fact]
    public void Diff_DetectsAddedSteps()
    {
        var wf1 = MakeWorkflow(steps: [new() { Name = "A", Type = "Action" }]);
        _store.CreateVersion(wf1, changeSummary: "v1");

        var wf2 = MakeWorkflow(steps:
        [
            new() { Name = "A", Type = "Action" },
            new() { Name = "B", Type = "Action" }
        ]);
        _store.CreateVersion(wf2, changeSummary: "v2");

        var diff = _store.Diff("wf-1", 1, 2);
        diff.Should().NotBeNull();
        diff!.AddedSteps.Should().HaveCount(1);
        diff.AddedSteps[0].Name.Should().Be("B");
    }

    [Fact]
    public void Diff_DetectsRemovedSteps()
    {
        var wf1 = MakeWorkflow(steps:
        [
            new() { Name = "A", Type = "Action" },
            new() { Name = "B", Type = "Action" }
        ]);
        _store.CreateVersion(wf1, changeSummary: "v1");

        var wf2 = MakeWorkflow(steps: [new() { Name = "A", Type = "Action" }]);
        _store.CreateVersion(wf2, changeSummary: "v2");

        var diff = _store.Diff("wf-1", 1, 2);
        diff!.RemovedSteps.Should().HaveCount(1);
        diff.RemovedSteps[0].Name.Should().Be("B");
    }

    [Fact]
    public void Diff_DetectsNameChange()
    {
        _store.CreateVersion(MakeWorkflow(name: "Old"), changeSummary: "v1");
        _store.CreateVersion(MakeWorkflow(name: "New"), changeSummary: "v2");

        var diff = _store.Diff("wf-1", 1, 2);
        diff!.NameChanged.Should().BeTrue();
        diff.OldName.Should().Be("Old");
        diff.NewName.Should().Be("New");
    }

    [Fact]
    public void Diff_ReturnsNull_WhenVersionNotFound()
    {
        _store.Diff("wf-1", 1, 2).Should().BeNull();
    }

    public void Dispose()
    {
        _db.Dispose();
        _factory.Dispose();
    }
}

