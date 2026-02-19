using FluentAssertions;
using WorkflowFramework.Dashboard.Api.Models;
using WorkflowFramework.Dashboard.Api.Services;
using WorkflowFramework.Serialization;

namespace WorkflowFramework.Dashboard.Api.Tests;

public class WorkflowRunServiceTests
{
    private readonly InMemoryWorkflowDefinitionStore _store = new();
    private readonly WorkflowRunService _runService;

    public WorkflowRunServiceTests()
    {
        _runService = new WorkflowRunService(_store);
    }

    private async Task<SavedWorkflowDefinition> CreateWorkflow()
    {
        return await _store.CreateAsync(new CreateWorkflowRequest
        {
            Description = "Test",
            Definition = new WorkflowDefinitionDto
            {
                Name = "TestWorkflow",
                Steps = [new StepDefinitionDto { Name = "S1", Type = "Action" }]
            }
        });
    }

    [Fact]
    public async Task StartRunAsync_ReturnsNullForMissingWorkflow()
    {
        var result = await _runService.StartRunAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task StartRunAsync_CreatesRun()
    {
        var wf = await CreateWorkflow();
        var run = await _runService.StartRunAsync(wf.Id);

        run.Should().NotBeNull();
        run!.RunId.Should().NotBeNullOrEmpty();
        run.WorkflowId.Should().Be(wf.Id);
        run.WorkflowName.Should().Be("TestWorkflow");
        run.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task GetRunsAsync_ReturnsAllRuns()
    {
        var wf = await CreateWorkflow();
        await _runService.StartRunAsync(wf.Id);
        await _runService.StartRunAsync(wf.Id);

        var runs = await _runService.GetRunsAsync();
        runs.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRunAsync_ReturnsNullForMissing()
    {
        var result = await _runService.GetRunAsync("nonexistent");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRunAsync_ReturnsExistingRun()
    {
        var wf = await CreateWorkflow();
        var created = await _runService.StartRunAsync(wf.Id);
        var fetched = await _runService.GetRunAsync(created!.RunId);

        fetched.Should().NotBeNull();
        fetched!.RunId.Should().Be(created.RunId);
    }

    [Fact]
    public async Task CancelRunAsync_ReturnsFalseForMissing()
    {
        var result = await _runService.CancelRunAsync("nonexistent");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CancelRunAsync_ReturnsTrueForExisting()
    {
        var wf = await CreateWorkflow();
        var run = await _runService.StartRunAsync(wf.Id);
        var result = await _runService.CancelRunAsync(run!.RunId);

        result.Should().BeTrue();
    }
}
